using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EasySave.Data.Configuration;
using EasySave.Protocol.Messages;
using EasySave.Protocol.Transport;

namespace EasySave.Models.Logger;

/// <summary>
///     Singleton TCP client used by EasySave to communicate with the central server.
///     Supports legacy write-only log frames and bidirectional protocol envelopes.
/// </summary>
public sealed class NetworkLog
{
    private static readonly Lazy<NetworkLog> InstanceFactory = new(() => new NetworkLog());

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly object _sync = new();
    private readonly object _sendSync = new();
    private ProtocolMessageChannel? _channel;
    private bool _manualClose;
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private CancellationTokenSource? _receiveLoopCts;
    private Task? _receiveLoopTask;
    private TcpClient? _tcpClient;

    private NetworkLog()
    {
    }

    public static NetworkLog Instance => InstanceFactory.Value;

    public event EventHandler? OnConnect;
    public event EventHandler? OnDisconnect;
    public event EventHandler<ProtocolEnvelope>? OnEnvelopeReceived;

    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _tcpClient is { Connected: true };
            }
        }
    }

    public void CreateSocket()
    {
        _manualClose = false;
        lock (_sync)
        {
            CloseSocketCore(raiseDisconnect: false);

            try
            {
                var config = ApplicationConfiguration.Load();
                var endpoint = new IPEndPoint(IPAddress.Parse(config.EasySaveServerIp), config.EasySaveServerPort);

                _tcpClient = new TcpClient();
                _tcpClient.Connect(endpoint);

                _channel = new ProtocolMessageChannel(_tcpClient.GetStream());
                _receiveLoopCts = new CancellationTokenSource();
                _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCts.Token));
                CancelReconnectLoop();

                OnConnect?.Invoke(this, EventArgs.Empty);
                Console.WriteLine("Socket created and ready to use.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating socket: {ex.Message}");
                CloseSocketCore(raiseDisconnect: true);
                StartReconnectLoop();
            }
        }
    }

    public void CloseSocket()
    {
        _manualClose = true;
        lock (_sync)
        {
            CancelReconnectLoop();
            CloseSocketCore(raiseDisconnect: true);
        }
    }

    public void Log<T>(T message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var frame = JsonSerializer.Serialize(message, _jsonOptions);
        try
        {
            SendRawFrame(frame);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            CreateSocket();
        }
    }

    public void SendEnvelope(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            SendEnvelopeCore(envelope);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending protocol envelope: {ex.Message}");
            CreateSocket();
        }
    }

    private void SendEnvelopeCore(ProtocolEnvelope envelope)
    {
        lock (_sendSync)
        {
            ProtocolMessageChannel? channel;
            lock (_sync)
            {
                channel = _channel;
            }

            if (channel == null)
                throw new InvalidOperationException("TCP client is not connected.");

            channel.SendAsync(envelope).GetAwaiter().GetResult();
        }
    }

    private void SendRawFrame(string frame)
    {
        lock (_sendSync)
        {
            TcpClient? tcpClient;
            lock (_sync)
            {
                tcpClient = _tcpClient;
            }

            if (tcpClient is not { Connected: true })
                throw new InvalidOperationException("TCP client is not connected.");

            var stream = tcpClient.GetStream();
            LengthPrefixedMessageFraming.WriteFrameAsync(stream, frame).GetAwaiter().GetResult();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ProtocolMessageChannel? channel;
                lock (_sync)
                {
                    channel = _channel;
                }

                if (channel == null)
                    return;

                var envelope = await channel.ReceiveAsync(cancellationToken);
                if (envelope == null)
                    break;

                OnEnvelopeReceived?.Invoke(this, envelope);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when connection is intentionally closed.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Socket receive loop stopped: {ex.Message}");
        }
        finally
        {
            lock (_sync)
            {
                CloseSocketCore(raiseDisconnect: true);
            }

            StartReconnectLoop();
        }
    }

    private void StartReconnectLoop()
    {
        if (_manualClose || ApplicationConfiguration.Load().RoutingType == RoutingType.Local)
            return;

        lock (_sync)
        {
            if (_reconnectTask is { IsCompleted: false })
                return;

            _reconnectCts?.Dispose();
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            _reconnectTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), token);
                        if (IsConnected)
                            return;

                        CreateSocket();
                        if (IsConnected)
                            return;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        // Keep retrying until cancellation.
                    }
                }
            }, token);
        }
    }

    private void CancelReconnectLoop()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
        _reconnectTask = null;
    }

    private void CloseSocketCore(bool raiseDisconnect)
    {
        _receiveLoopCts?.Cancel();
        _receiveLoopCts?.Dispose();
        _receiveLoopCts = null;
        _receiveLoopTask = null;

        _channel = null;

        if (_tcpClient != null)
        {
            try
            {
                _tcpClient.Close();
                Console.WriteLine("Socket closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when closing socket: {ex.Message}");
            }
            finally
            {
                _tcpClient = null;
            }
        }

        if (raiseDisconnect)
            OnDisconnect?.Invoke(this, EventArgs.Empty);
    }
}
