using EasySave.Protocol.Messages;

namespace EasySave.RemoteConsole.ViewModels;

public partial class RemoteJobItemViewModel : ViewModelBase
{
    private DateTimeOffset _lastUpdatedAtUtc = DateTimeOffset.MinValue;

    public int JobId { get; }
    public string JobName { get; }

    public RemoteJobItemViewModel(int jobId, string jobName)
    {
        JobId = jobId;
        JobName = jobName;
    }

    public RemoteJobStatus Status { get; private set; }
    public double Progress { get; private set; }
    public int CurrentFileIndex { get; private set; }
    public int FilesCount { get; private set; }
    public long TransferredSize { get; private set; }
    public long TotalSize { get; private set; }
    public string StatusText { get; private set; } = "Idle";
    public string LastCommandAck { get; private set; } = string.Empty;

    public bool CanPause => Status == RemoteJobStatus.Running;
    public bool CanResume => Status == RemoteJobStatus.Paused;
    public bool CanStop => Status is RemoteJobStatus.Running or RemoteJobStatus.Paused;

    public void ApplySnapshot(BackupJobSnapshotMessage snapshot)
    {
        ApplyStatus(snapshot.Status, snapshot.CurrentProgress, snapshot.CurrentFileIndex, snapshot.FilesCount,
            snapshot.TransferredSize, snapshot.TotalSize, snapshot.UpdatedAtUtc, null);
    }

    public void ApplyProgress(ProgressUpdateMessage progress)
    {
        ApplyStatus(progress.Status, progress.CurrentProgress, progress.CurrentFileIndex, progress.FilesCount,
            progress.TransferredSize, progress.TotalSize, progress.UpdatedAtUtc, progress.StatusDetail);
    }

    public void ApplyCommandResult(CommandResultMessage result)
    {
        LastCommandAck = $"{result.Command}: {result.Status}{(string.IsNullOrWhiteSpace(result.Message) ? string.Empty : $" - {result.Message}")}";
        OnPropertyChanged(nameof(LastCommandAck));
    }

    private void ApplyStatus(RemoteJobStatus status, double progress, int currentFileIndex, int filesCount, long transferredSize,
        long totalSize, DateTimeOffset updatedAtUtc, string? detail)
    {
        if (updatedAtUtc < _lastUpdatedAtUtc)
            return;

        _lastUpdatedAtUtc = updatedAtUtc;
        Status = status;
        Progress = Math.Clamp(progress, 0, 100);
        CurrentFileIndex = currentFileIndex;
        FilesCount = filesCount;
        TransferredSize = transferredSize;
        TotalSize = totalSize;
        StatusText = string.IsNullOrWhiteSpace(detail) ? status.ToString() : $"{status} - {detail}";

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(CurrentFileIndex));
        OnPropertyChanged(nameof(FilesCount));
        OnPropertyChanged(nameof(TransferredSize));
        OnPropertyChanged(nameof(TotalSize));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanStop));
    }
}
