using System.Net.Sockets;
using EasySave.Protocol.Messages;
using EasySave.Protocol.Transport;

namespace EasySave.RemoteConsole.Services;

public sealed class RemoteConsoleClientService : IRemoteConsoleClientService
{
    private readonly string _consoleId = $"console-{Environment.MachineName}-{Guid.NewGuid():N}";
    private readonly SemaphoreSlim _sync = new(1, 1);

    private ProtocolMessageChannel? _channel;
    private TcpClient? _tcpClient;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;

    private bool _isDisposed;
    private bool _intentionalClose;
    private string? _serverIp;
    private int _serverPort;
    private string? _selectedHostId;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<HostRegistrationMessage>? HostDiscovered;
    public event EventHandler<BackupJobSnapshotMessage>? JobSnapshotReceived;
    public event EventHandler<ProgressUpdateMessage>? ProgressReceived;
    public event EventHandler<CommandResultMessage>? CommandResultReceived;
    public event EventHandler<ErrorMessage>? ErrorReceived;

    public bool IsConnected { get; private set; }

    public async Task ConnectAsync(string serverIp, int serverPort, string? requestedInstanceId, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            _serverIp = serverIp;
            _serverPort = serverPort;
            _selectedHostId = string.IsNullOrWhiteSpace(requestedInstanceId) ? null : requestedInstanceId;

            _intentionalClose = true;
            await CloseConnectionCoreAsync();
            _intentionalClose = false;
            await OpenConnectionCoreAsync(cancellationToken);
            await SendRemoteRegistrationAsync(cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task SelectHostAsync(string? instanceId, CancellationToken cancellationToken = default)
    {
        _selectedHostId = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId;
        if (!IsConnected)
            return;

        await SendRemoteRegistrationAsync(cancellationToken);
    }

    public async Task SendCommandAsync(string instanceId, int jobId, string jobName, CommandType command, string requestedBy, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _channel is null)
            throw new InvalidOperationException("Not connected to server.");

        var request = new CommandRequestMessage(
            Guid.NewGuid().ToString("N"),
            instanceId,
            jobId,
            jobName,
            command,
            requestedBy,
            DateTimeOffset.UtcNow);

        var envelope = ProtocolSerializer.CreateEnvelope(ProtocolMessageTypes.CommandRequest, _consoleId, request);
        await _channel.SendAsync(envelope, cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        await _sync.WaitAsync();
        try
        {
            _intentionalClose = true;
            await CloseConnectionCoreAsync();
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task OpenConnectionCoreAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_serverIp) || _serverPort <= 0)
            throw new InvalidOperationException("Missing server endpoint.");

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(_serverIp, _serverPort, cancellationToken);
        _channel = new ProtocolMessageChannel(_tcpClient.GetStream());

        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);

        SetConnectionState(true);
    }

    private async Task SendRemoteRegistrationAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected || _channel is null)
            return;

        var registration = new RemoteConsoleRegistrationMessage(
            _consoleId,
            Environment.MachineName,
            typeof(RemoteConsoleClientService).Assembly.GetName().Version?.ToString() ?? "unknown",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            _selectedHostId);

        var envelope = ProtocolSerializer.CreateEnvelope(
            ProtocolMessageTypes.RemoteConsoleRegistration,
            _consoleId,
            registration);

        await _channel.SendAsync(envelope, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var channel = _channel;
                if (channel is null)
                    break;

                var envelope = await channel.ReceiveAsync(cancellationToken);
                if (envelope is null)
                    break;

                HandleEnvelope(envelope);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on disconnect
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke(this, new ErrorMessage(_consoleId, "REMOTE_RECEIVE_ERROR", ex.Message, DateTimeOffset.UtcNow));
        }
        finally
        {
            if (!_isDisposed && !_intentionalClose)
                await TryReconnectAsync(cancellationToken);
        }
    }

    private void HandleEnvelope(ProtocolEnvelope envelope)
    {
        switch (envelope.MessageType)
        {
            case ProtocolMessageTypes.HostRegistration:
                var host = ProtocolSerializer.DeserializePayload<HostRegistrationMessage>(envelope);
                if (host is not null)
                    HostDiscovered?.Invoke(this, host);
                break;

            case ProtocolMessageTypes.BackupJobSnapshot:
                var snapshot = ProtocolSerializer.DeserializePayload<BackupJobSnapshotMessage>(envelope);
                if (snapshot is not null)
                    JobSnapshotReceived?.Invoke(this, snapshot);
                break;

            case ProtocolMessageTypes.ProgressUpdate:
                var progress = ProtocolSerializer.DeserializePayload<ProgressUpdateMessage>(envelope);
                if (progress is not null)
                    ProgressReceived?.Invoke(this, progress);
                break;

            case ProtocolMessageTypes.CommandResult:
                var command = ProtocolSerializer.DeserializePayload<CommandResultMessage>(envelope);
                if (command is not null)
                    CommandResultReceived?.Invoke(this, command);
                break;

            case ProtocolMessageTypes.Error:
                var error = ProtocolSerializer.DeserializePayload<ErrorMessage>(envelope);
                if (error is not null)
                    ErrorReceived?.Invoke(this, error);
                break;
        }
    }

    private async Task TryReconnectAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await CloseConnectionCoreAsync();

            if (string.IsNullOrWhiteSpace(_serverIp) || _serverPort <= 0)
                return;
        }
        finally
        {
            _sync.Release();
        }

        while (!_isDisposed)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                await _sync.WaitAsync(cancellationToken);
                try
                {
                    await OpenConnectionCoreAsync(cancellationToken);
                    await SendRemoteRegistrationAsync(cancellationToken);
                    return;
                }
                finally
                {
                    _sync.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                SetConnectionState(false);
            }
        }
    }

    private async Task CloseConnectionCoreAsync()
    {
        _receiveCts?.Cancel();

        var receiveTask = _receiveTask;
        if (receiveTask is not null)
        {
            try
            {
                await Task.WhenAny(receiveTask, Task.Delay(500));
            }
            catch
            {
                // ignore cleanup race exceptions
            }
        }

        _receiveTask = null;
        _receiveCts?.Dispose();
        _receiveCts = null;

        _channel = null;

        if (_tcpClient is not null)
        {
            _tcpClient.Dispose();
            _tcpClient = null;
        }

        SetConnectionState(false);
    }

    private void SetConnectionState(bool connected)
    {
        if (IsConnected == connected)
            return;

        IsConnected = connected;
        ConnectionStateChanged?.Invoke(this, connected);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        await DisconnectAsync();
        _sync.Dispose();
    }
}
