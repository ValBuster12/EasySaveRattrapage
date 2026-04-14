using EasySave.Models.Backup.Execution;
using EasySave.Protocol.Messages;
using EasySave.ViewModels;

namespace EasySave.Models.Network;

public interface IBackupHostCommunicationService
{
    /// <summary>
    ///     Raised when a remote console command must be handled by the host.
    /// </summary>
    event Func<CommandRequestMessage, Task<CommandResultMessage>>? RemoteCommandReceived;

    /// <summary>
    ///     Subscribes to network events and registers the host endpoint.
    /// </summary>
    void Start();
    /// <summary>
    ///     Unsubscribes from network events.
    /// </summary>
    void Stop();
    void PublishJobCatalog(IEnumerable<BackupJobItemViewModel> jobs);
    void PublishJobLifecycle(BackupJob job, RemoteJobStatus status, string? detail = null);
    void PublishProgress(BackupJob job, BackupExecutionProgressSnapshot snapshot, RemoteJobStatus status, string? detail = null);
    void PublishCommandResult(CommandResultMessage result, Guid? correlationId = null);
}
