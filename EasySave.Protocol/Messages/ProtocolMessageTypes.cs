namespace EasySave.Protocol.Messages;

public static class ProtocolMessageTypes
{
    public const string HelloRegister = "hello.register";
    public const string JobStateSnapshot = "job.state.snapshot";
    public const string ProgressUpdate = "job.progress.update";
    public const string CommandRequest = "job.command.request";
    public const string CommandAcknowledgment = "job.command.ack";
    public const string Heartbeat = "connection.heartbeat";
    public const string Disconnect = "connection.disconnect";
}
