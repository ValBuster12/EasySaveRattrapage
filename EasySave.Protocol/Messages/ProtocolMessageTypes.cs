namespace EasySave.Protocol.Messages;

public static class ProtocolMessageTypes
{
    public const string HostRegistration = "host.registration";
    public const string RemoteConsoleRegistration = "remoteConsole.registration";
    public const string BackupJobSnapshot = "backupJob.snapshot";
    public const string ProgressUpdate = "backupJob.progressUpdate";
    public const string CommandRequest = "command.request";
    public const string CommandResult = "command.result";
    public const string Heartbeat = "connection.heartbeat";
    public const string Error = "connection.error";
}
