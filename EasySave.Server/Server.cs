using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EasyLog;
using EasySave.Log.Model;
using EasySave.Protocol.Messages;
using EasySave.Protocol.Transport;

namespace EasySave.Server;

/// <summary>
///     Main class for the TCP server that receives messages.
/// </summary>
public static class Server
{
    private static AbstractLogger<LogEntry> _logger;
    private static TcpListener _tcpServer;

    public static void Main(string[] args)
    {
        StartServer();

        var logType = Environment.GetEnvironmentVariable("EasySaveLogType");
        if (logType == "xml")
            _logger = new XmlLogger<LogEntry>("./logs/");
        else
            _logger = new JsonLogger<LogEntry>("./logs/");

        Console.WriteLine("Server is listening for TCP messages...");

        while (true)
            ListenForClients();
    }

    private static void StartServer()
    {
        _tcpServer = new TcpListener(IPAddress.Any, 5000);
        _tcpServer.Start();
    }

    private static void ListenForClients()
    {
        try
        {
            using var client = _tcpServer.AcceptTcpClient();
            using var networkStream = client.GetStream();

            var frame = LengthPrefixedMessageFraming.ReadFrameAsync(networkStream).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(frame))
            {
                Console.WriteLine("Received empty frame.");
                return;
            }

            if (TryHandleProtocolEnvelope(frame, (IPEndPoint)client.Client.RemoteEndPoint!))
                return;

            HandleLegacyLogEntry(frame, (IPEndPoint)client.Client.RemoteEndPoint!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    private static bool TryHandleProtocolEnvelope(string frame, IPEndPoint remoteEndPoint)
    {
        var envelope = ProtocolSerializer.DeserializeEnvelope(frame);
        if (envelope == null || string.IsNullOrWhiteSpace(envelope.MessageType))
            return false;

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{remoteEndPoint.Address}] Protocol message '{envelope.MessageType}' from '{envelope.SenderId}'.");
        return true;
    }

    private static void HandleLegacyLogEntry(string json, IPEndPoint remoteEndPoint)
    {
        var entry = JsonSerializer.Deserialize<LogEntry>(json);
        if (entry == null)
        {
            Console.WriteLine("Received message is neither protocol envelope nor legacy log entry.");
            return;
        }

        entry.ClientIPAddress = remoteEndPoint.Address.ToString();
        var logMessage = FormatLogMessage(entry.ToString(), remoteEndPoint);
        Console.WriteLine(logMessage);
        Log(entry);
    }

    private static string FormatLogMessage(string message, IPEndPoint client)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var clientIp = client.Address.ToString();
        return $"[{timestamp}] [{clientIp}] {message}";
    }

    private static void Log(LogEntry message)
    {
        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        _logger.Log(message);
    }
}
