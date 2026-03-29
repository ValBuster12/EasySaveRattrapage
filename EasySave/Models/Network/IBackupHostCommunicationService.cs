using EasySave.Models.Backup.Execution;
using EasySave.Protocol.Messages;
using EasySave.ViewModels;

namespace EasySave.Models.Network;

public interface IBackupHostCommunicationService
{
    event Func<CommandRequestMessage, Task<CommandResultMessage>>? RemoteCommandReceived;

    void Start();
    void Stop();
    void PublishJobCatalog(IEnumerable<BackupJobItemViewModel> jobs);
    void PublishJobLifecycle(BackupJob job, RemoteJobStatus status, string? detail = null);
    void PublishProgress(BackupJob job, BackupExecutionProgressSnapshot snapshot, RemoteJobStatus status, string? detail = null);
    void PublishCommandResult(CommandResultMessage result, Guid? correlationId = null);
}
