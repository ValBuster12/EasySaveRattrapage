using EasySave.Protocol.Messages;

namespace EasySave.RemoteConsole.Services;

public interface IRemoteConsoleClientService : IAsyncDisposable
{
    event EventHandler<bool>? ConnectionStateChanged;
    event EventHandler<HostRegistrationMessage>? HostDiscovered;
    event EventHandler<BackupJobSnapshotMessage>? JobSnapshotReceived;
    event EventHandler<ProgressUpdateMessage>? ProgressReceived;
    event EventHandler<CommandResultMessage>? CommandResultReceived;
    event EventHandler<ErrorMessage>? ErrorReceived;

    bool IsConnected { get; }

    Task ConnectAsync(string serverIp, int serverPort, string? requestedInstanceId, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SelectHostAsync(string? instanceId, CancellationToken cancellationToken = default);
    Task SendCommandAsync(string instanceId, int jobId, string jobName, CommandType command, string requestedBy, CancellationToken cancellationToken = default);
}
