using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EasySave.Protocol.Messages;
using EasySave.Protocol.Transport;

namespace EasySave.Server;

/// <summary>
/// EasySave TCP broker for the remote console feature.
///
/// Startup flow:
/// 1) Resolve IP and port from command-line args (or defaults).
/// 2) Start TcpListener.
/// 3) Accept each TCP client asynchronously and process messages in its own task.
/// 4) Register client role using protocol registration messages.
/// 5) Route protocol messages between Host and RemoteConsole clients without business logic.
/// </summary>
public static class Server
{
    private const int DefaultPort = 5000;
    private static readonly IPAddress DefaultIp = IPAddress.Any;

    private static readonly ConcurrentDictionary<string, HostSession> HostsByInstanceId = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<Guid, ClientSession> SessionsByConnectionId = new();
    private static readonly ConcurrentDictionary<Guid, string> RemoteSubscriptions = new();

    public static async Task Main(string[] args)
    {
        var (ipAddress, port, routingMode) = ParseStartupConfiguration(args);
        using var listener = new TcpListener(ipAddress, port);
        listener.Start();

        LogInfo($"Broker started on {ipAddress}:{port} (routing={routingMode})");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
            LogInfo("Shutdown requested (Ctrl+C).");
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
                _ = Task.Run(() => HandleClientAsync(tcpClient, cts.Token), cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        finally
        {
            listener.Stop();
            LogInfo("Broker stopped.");
        }
    }

    private static async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid();
        var remoteEndPoint = (IPEndPoint?)tcpClient.Client.RemoteEndPoint;
        var endpointText = remoteEndPoint is null ? "unknown" : $"{remoteEndPoint.Address}:{remoteEndPoint.Port}";

        var session = new ClientSession(connectionId, endpointText, tcpClient);
        SessionsByConnectionId[connectionId] = session;
        LogInfo($"Connect: {connectionId} from {endpointText}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var envelope = await session.Channel.ReceiveAsync(cancellationToken);
                if (envelope is null)
                {
                    break;
                }

                await HandleEnvelopeAsync(session, envelope, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            LogError($"Connection error on {connectionId}: {ex.Message}");
        }
        finally
        {
            await CleanupConnectionAsync(session, cancellationToken);
            SessionsByConnectionId.TryRemove(connectionId, out _);
            tcpClient.Dispose();
            LogInfo($"Disconnect: {connectionId} from {endpointText}");
        }
    }

    private static async Task HandleEnvelopeAsync(
        ClientSession session,
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (envelope.MessageType)
            {
                case ProtocolMessageTypes.HostRegistration:
                    await RegisterHostAsync(session, envelope, cancellationToken);
                    break;

                case ProtocolMessageTypes.RemoteConsoleRegistration:
                    await RegisterRemoteConsoleAsync(session, envelope, cancellationToken);
                    break;

                case ProtocolMessageTypes.BackupJobSnapshot:
                case ProtocolMessageTypes.ProgressUpdate:
                case ProtocolMessageTypes.CommandResult:
                    await RelayHostToSubscribersAsync(session, envelope, cancellationToken);
                    break;

                case ProtocolMessageTypes.CommandRequest:
                    await RelayRemoteToHostAsync(session, envelope, cancellationToken);
                    break;

                default:
                    await SendErrorAsync(
                        session.Channel,
                        "server",
                        "UNSUPPORTED_MESSAGE",
                        $"Unsupported message type '{envelope.MessageType}'.",
                        envelope.MessageId,
                        cancellationToken);
                    LogError($"Unsupported message '{envelope.MessageType}' from {session.ConnectionId}");
                    break;
            }
        }
        catch (Exception ex)
        {
            await SendErrorAsync(
                session.Channel,
                "server",
                "PROCESSING_ERROR",
                "Message processing failed.",
                envelope.MessageId,
                cancellationToken,
                ex.Message);

            LogError($"Processing error for {session.ConnectionId}: {ex.Message}");
        }
    }

    private static async Task RegisterHostAsync(
        ClientSession session,
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var registration = ProtocolSerializer.DeserializePayload<HostRegistrationMessage>(envelope);
        if (registration is null || string.IsNullOrWhiteSpace(registration.InstanceId))
        {
            return SendErrorAsync(session.Channel, "server", "INVALID_HOST_REGISTRATION", "Invalid host registration payload.", envelope.MessageId, cancellationToken);
        }

        session.Role = ProtocolClientKind.EasySaveHost;
        session.ClientId = envelope.SenderId;
        session.InstanceId = registration.InstanceId;

        HostsByInstanceId[registration.InstanceId] = new HostSession(registration, session.ConnectionId, session);
        LogInfo($"Register host: connection={session.ConnectionId}, instanceId={registration.InstanceId}, senderId={envelope.SenderId}");

        await BroadcastHostRegistrationAsync(envelope, cancellationToken);
    }

    private static async Task RegisterRemoteConsoleAsync(
        ClientSession session,
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var registration = ProtocolSerializer.DeserializePayload<RemoteConsoleRegistrationMessage>(envelope);
        if (registration is null)
        {
            await SendErrorAsync(session.Channel, "server", "INVALID_REMOTE_REGISTRATION", "Invalid remote console registration payload.", envelope.MessageId, cancellationToken);
            return;
        }

        session.Role = ProtocolClientKind.RemoteConsole;
        session.ClientId = envelope.SenderId;

        if (!string.IsNullOrWhiteSpace(registration.RequestedInstanceId))
        {
            RemoteSubscriptions[session.ConnectionId] = registration.RequestedInstanceId;
            LogInfo($"Subscribe: remote={session.ConnectionId} -> instanceId={registration.RequestedInstanceId}");

            if (!HostsByInstanceId.ContainsKey(registration.RequestedInstanceId))
            {
                await SendErrorAsync(
                    session.Channel,
                    "server",
                    "HOST_OFFLINE",
                    $"Host '{registration.RequestedInstanceId}' is not currently connected.",
                    envelope.MessageId,
                    cancellationToken);
            }
        }
        else
        {
            RemoteSubscriptions.TryRemove(session.ConnectionId, out _);
        }

        foreach (var host in HostsByInstanceId.Values)
        {
            var hostRegistration = new HostRegistrationMessage(
                host.Registration.InstanceId,
                host.Registration.HostName,
                host.Registration.ApplicationVersion,
                host.Registration.StartedAtUtc,
                DateTimeOffset.UtcNow,
                host.Registration.Capabilities);

            var hostEnvelope = ProtocolSerializer.CreateEnvelope(
                ProtocolMessageTypes.HostRegistration,
                host.Registration.InstanceId,
                hostRegistration);

            await session.Channel.SendAsync(hostEnvelope, cancellationToken);
        }

        LogInfo($"Register remote console: connection={session.ConnectionId}, senderId={envelope.SenderId}");
    }

    private static async Task BroadcastHostRegistrationAsync(ProtocolEnvelope envelope, CancellationToken cancellationToken)
    {
        var remotes = SessionsByConnectionId.Values.Where(s => s.Role == ProtocolClientKind.RemoteConsole).ToList();

        foreach (var remote in remotes)
        {
            try
            {
                await remote.Channel.SendAsync(envelope, cancellationToken);
            }
            catch (Exception ex)
            {
                LogError($"Failed to broadcast host registration to remote {remote.ConnectionId}: {ex.Message}");
            }
        }
    }

    private static async Task RelayHostToSubscribersAsync(
        ClientSession hostSession,
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (hostSession.Role != ProtocolClientKind.EasySaveHost || string.IsNullOrWhiteSpace(hostSession.InstanceId))
        {
            LogError($"Relay denied: host-only message '{envelope.MessageType}' from unregistered connection {hostSession.ConnectionId}");
            return;
        }

        var instanceId = hostSession.InstanceId;
        var targetRemotes = SessionsByConnectionId.Values
            .Where(s => s.Role == ProtocolClientKind.RemoteConsole
                        && RemoteSubscriptions.TryGetValue(s.ConnectionId, out var subscribed)
                        && string.Equals(subscribed, instanceId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var remoteSession in targetRemotes)
        {
            try
            {
                await remoteSession.Channel.SendAsync(envelope, cancellationToken);
                LogInfo($"Relay: host({instanceId}) -> remote({remoteSession.ConnectionId}) type={envelope.MessageType}");
            }
            catch (Exception ex)
            {
                LogError($"Relay failure host->remote: host={hostSession.ConnectionId}, remote={remoteSession.ConnectionId}, error={ex.Message}");
            }
        }
    }

    private static async Task RelayRemoteToHostAsync(
        ClientSession remoteSession,
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (remoteSession.Role != ProtocolClientKind.RemoteConsole)
        {
            LogError($"Relay denied: remote-only message '{envelope.MessageType}' from unregistered connection {remoteSession.ConnectionId}");
            return;
        }

        var request = ProtocolSerializer.DeserializePayload<CommandRequestMessage>(envelope);
        if (request is null || string.IsNullOrWhiteSpace(request.TargetInstanceId))
        {
            await SendErrorAsync(remoteSession.Channel, "server", "INVALID_COMMAND_REQUEST", "Invalid command request payload.", envelope.MessageId, cancellationToken);
            return;
        }

        if (!HostsByInstanceId.TryGetValue(request.TargetInstanceId, out var host))
        {
            await SendErrorAsync(
                remoteSession.Channel,
                "server",
                "HOST_OFFLINE",
                $"Host '{request.TargetInstanceId}' is not currently connected.",
                envelope.MessageId,
                cancellationToken);
            return;
        }

        await host.Session.Channel.SendAsync(envelope, cancellationToken);
        LogInfo($"Relay: remote({remoteSession.ConnectionId}) -> host({request.TargetInstanceId}) type={envelope.MessageType}");
    }

    private static async Task CleanupConnectionAsync(ClientSession session, CancellationToken cancellationToken)
    {
        if (session.Role == ProtocolClientKind.EasySaveHost && !string.IsNullOrWhiteSpace(session.InstanceId))
        {
            HostsByInstanceId.TryRemove(session.InstanceId, out _);
            LogInfo($"Unregister host: instanceId={session.InstanceId}, connection={session.ConnectionId}");

            var impactedRemotes = SessionsByConnectionId.Values
                .Where(s => s.Role == ProtocolClientKind.RemoteConsole
                            && RemoteSubscriptions.TryGetValue(s.ConnectionId, out var subscribed)
                            && string.Equals(subscribed, session.InstanceId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var remote in impactedRemotes)
            {
                try
                {
                    await SendErrorAsync(
                        remote.Channel,
                        "server",
                        "HOST_DISCONNECTED",
                        $"Host '{session.InstanceId}' disconnected.",
                        null,
                        cancellationToken);
                    LogInfo($"Notify remote {remote.ConnectionId}: host '{session.InstanceId}' disconnected");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to notify remote {remote.ConnectionId}: {ex.Message}");
                }
            }
        }

        if (session.Role == ProtocolClientKind.RemoteConsole)
        {
            RemoteSubscriptions.TryRemove(session.ConnectionId, out _);
            LogInfo($"Unregister remote console: connection={session.ConnectionId}");
        }
    }

    private static async Task SendErrorAsync(
        ProtocolMessageChannel channel,
        string senderId,
        string code,
        string message,
        Guid? relatedMessageId,
        CancellationToken cancellationToken,
        string? details = null)
    {
        var payload = new ErrorMessage(
            senderId,
            code,
            message,
            DateTimeOffset.UtcNow,
            details,
            relatedMessageId?.ToString());

        var envelope = ProtocolSerializer.CreateEnvelope(
            ProtocolMessageTypes.Error,
            senderId,
            payload,
            correlationId: relatedMessageId);

        await channel.SendAsync(envelope, cancellationToken);
    }

    private static (IPAddress ipAddress, int port, string routingMode) ParseStartupConfiguration(string[] args)
    {
        var fileConfig = ServerConfiguration.Load();

        var ip = GetArgument(args, "--ip")
                 ?? Environment.GetEnvironmentVariable("EASYSAVE_SERVER_IP")
                 ?? fileConfig.ServerIp;
        var portValue = GetArgument(args, "--port")
                        ?? Environment.GetEnvironmentVariable("EASYSAVE_SERVER_PORT")
                        ?? fileConfig.ServerPort.ToString();
        var routingMode = GetArgument(args, "--routing")
                          ?? Environment.GetEnvironmentVariable("EASYSAVE_SERVER_ROUTING_MODE")
                          ?? fileConfig.RoutingMode;

        var ipAddress = IPAddress.TryParse(ip, out var parsedIp) ? parsedIp : DefaultIp;
        var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : DefaultPort;
        if (port <= 0 || port > 65535)
            port = DefaultPort;

        return (ipAddress, port, string.IsNullOrWhiteSpace(routingMode) ? "broker" : routingMode.Trim());
    }

    private static string? GetArgument(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void LogInfo(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:O}] [INFO] {message}");
    }

    private static void LogError(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:O}] [ERROR] {message}");
    }

    private sealed class ClientSession
    {
        public ClientSession(Guid connectionId, string endpoint, TcpClient tcpClient)
        {
            ConnectionId = connectionId;
            Endpoint = endpoint;
            TcpClient = tcpClient;
            Channel = new ProtocolMessageChannel(tcpClient.GetStream());
        }

        public Guid ConnectionId { get; }
        public string Endpoint { get; }
        public TcpClient TcpClient { get; }
        public ProtocolMessageChannel Channel { get; }

        public ProtocolClientKind? Role { get; set; }
        public string? ClientId { get; set; }
        public string? InstanceId { get; set; }
    }

    private sealed record HostSession(HostRegistrationMessage Registration, Guid ConnectionId, ClientSession Session);

    private sealed class ServerConfiguration
    {
        private const string ConfigFileName = "server.settings.json";

        public string ServerIp { get; init; } = "0.0.0.0";
        public int ServerPort { get; init; } = DefaultPort;
        public string RoutingMode { get; init; } = "broker";

        public static ServerConfiguration Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            if (!File.Exists(path))
            {
                var defaults = new ServerConfiguration();
                var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ServerConfiguration>(json) ?? new ServerConfiguration();
            }
            catch
            {
                return new ServerConfiguration();
            }
        }
    }
}
