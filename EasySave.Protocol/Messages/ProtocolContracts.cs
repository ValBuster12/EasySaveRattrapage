namespace EasySave.Protocol.Messages;

public enum ClientKind
{
    BackupHost,
    RemoteConsole,
    Broker
}

public enum RemoteJobRunState
{
    Idle,
    Running,
    Paused,
    Stopped,
    Completed,
    Failed
}

public enum BackupCommandType
{
    Pause,
    Resume,
    Stop
}

public enum CommandAckStatus
{
    Accepted,
    Rejected,
    Executed,
    Failed
}

public sealed record HelloRegisterMessage(
    ClientKind ClientKind,
    string ClientId,
    string DisplayName,
    string[] Capabilities,
    string? RequestedHostId = null);

public sealed record JobStateSnapshotMessage(
    string HostId,
    IReadOnlyList<JobStateSnapshotItem> Jobs);

public sealed record JobStateSnapshotItem(
    int JobId,
    string JobName,
    string SourceDirectory,
    string TargetDirectory,
    RemoteJobRunState State,
    double ProgressPercent,
    long TotalBytes,
    long TransferredBytes,
    int TotalFiles,
    int ProcessedFiles,
    bool CanPause,
    bool CanResume,
    bool CanStop,
    string? StatusMessage);

public sealed record ProgressUpdateMessage(
    string HostId,
    int JobId,
    RemoteJobRunState State,
    double ProgressPercent,
    long TotalBytes,
    long TransferredBytes,
    int TotalFiles,
    int ProcessedFiles,
    string? CurrentFile,
    string? StatusMessage);

public sealed record CommandRequestMessage(
    string HostId,
    int JobId,
    BackupCommandType Command,
    string RequestedBy,
    string? Reason = null);

public sealed record CommandAcknowledgmentMessage(
    string HostId,
    int JobId,
    BackupCommandType Command,
    CommandAckStatus Status,
    string? Message = null);

public sealed record HeartbeatMessage(
    ClientKind ClientKind,
    string ClientId,
    DateTimeOffset SentAtUtc);

public sealed record DisconnectMessage(
    ClientKind ClientKind,
    string ClientId,
    string Reason);
