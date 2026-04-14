namespace EasySave.Protocol.Messages;

public enum ProtocolClientKind
{
    EasySaveHost,
    RemoteConsole,
    Server
}

public enum RemoteJobStatus
{
    Idle,
    Running,
    Paused,
    Stopped,
    Completed,
    Failed
}

public enum CommandType
{
    Pause,
    Resume,
    Stop,
    Start
}

public enum CommandResultStatus
{
    Accepted,
    Rejected,
    Executed,
    Failed
}

public sealed record HostRegistrationMessage(
    string InstanceId,
    string HostName,
    string ApplicationVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset SentAtUtc,
    IReadOnlyList<string>? Capabilities = null);

public sealed record RemoteConsoleRegistrationMessage(
    string ConsoleId,
    string DisplayName,
    string ApplicationVersion,
    DateTimeOffset ConnectedAtUtc,
    DateTimeOffset SentAtUtc,
    string? RequestedInstanceId = null);

public sealed record BackupJobSnapshotMessage(
    string InstanceId,
    int JobId,
    string JobName,
    RemoteJobStatus Status,
    double CurrentProgress,
    int FilesCount,
    int CurrentFileIndex,
    long TransferredSize,
    long TotalSize,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset SentAtUtc);

public sealed record ProgressUpdateMessage(
    string InstanceId,
    int JobId,
    string JobName,
    RemoteJobStatus Status,
    double CurrentProgress,
    int FilesCount,
    int CurrentFileIndex,
    long TransferredSize,
    long TotalSize,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset SentAtUtc,
    string? CurrentFilePath = null,
    string? StatusDetail = null);

public sealed record CommandRequestMessage(
    string RequestId,
    string TargetInstanceId,
    int JobId,
    string JobName,
    CommandType Command,
    string RequestedBy,
    DateTimeOffset RequestedAtUtc,
    string? Reason = null);

public sealed record CommandResultMessage(
    string RequestId,
    string InstanceId,
    int JobId,
    string JobName,
    CommandType Command,
    CommandResultStatus Status,
    DateTimeOffset RespondedAtUtc,
    string? Message = null);

public sealed record HeartbeatMessage(
    ProtocolClientKind SenderKind,
    string SenderId,
    string? InstanceId,
    DateTimeOffset SentAtUtc);

public sealed record ErrorMessage(
    string SenderId,
    string ErrorCode,
    string Error,
    DateTimeOffset SentAtUtc,
    string? Details = null,
    string? RelatedRequestId = null);
