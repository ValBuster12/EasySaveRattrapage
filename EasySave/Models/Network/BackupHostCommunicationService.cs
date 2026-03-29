using EasySave.Models.Backup.Execution;
using EasySave.Data.Configuration;
using EasySave.Models.Logger;
using EasySave.Protocol.Messages;
using EasySave.Protocol.Transport;
using EasySave.ViewModels;

namespace EasySave.Models.Network;

/// <summary>
///     Dedicated host communication service that keeps protocol concerns outside ViewModels.
/// </summary>
public sealed class BackupHostCommunicationService : IBackupHostCommunicationService
{
    private const string StartCapability = "command.start";
    private const string PauseCapability = "command.pause";
    private const string ResumeCapability = "command.resume";
    private const string StopCapability = "command.stop";

    private readonly string _hostName;
    private readonly string _instanceId;
    private readonly NetworkLog _networkLog;

    public BackupHostCommunicationService()
        : this(BuildHostName(), BuildInstanceId(), NetworkLog.Instance)
    {
    }

    internal BackupHostCommunicationService(string hostName, string instanceId, NetworkLog networkLog)
    {
        _hostName = hostName;
        _instanceId = instanceId;
        _networkLog = networkLog;
    }

    public event Func<CommandRequestMessage, Task<CommandResultMessage>>? RemoteCommandReceived;

    public void Start()
    {
        _networkLog.OnConnect += OnSocketConnected;
        _networkLog.OnEnvelopeReceived += OnEnvelopeReceived;
        if (_networkLog.IsConnected)
            RegisterHost();
    }

    public void Stop()
    {
        _networkLog.OnConnect -= OnSocketConnected;
        _networkLog.OnEnvelopeReceived -= OnEnvelopeReceived;
    }

    public void PublishJobCatalog(IEnumerable<BackupJobItemViewModel> jobs)
    {
        foreach (var jobItem in jobs)
            PublishSnapshot(jobItem.Job, ResolveStatus(jobItem.Job), null);
    }

    public void PublishJobLifecycle(BackupJob job, RemoteJobStatus status, string? detail = null)
    {
        PublishSnapshot(job, status, detail);
    }

    public void PublishProgress(BackupJob job, BackupExecutionProgressSnapshot snapshot, RemoteJobStatus status, string? detail = null)
    {
        var message = new ProgressUpdateMessage(
            _instanceId,
            job.Id,
            job.Name,
            status,
            snapshot.CurrentProgress,
            snapshot.FilesCount,
            snapshot.CurrentFileIndex,
            snapshot.TransferredSize,
            snapshot.TotalSize,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            statusDetail: detail);

        SendEnvelope(ProtocolMessageTypes.ProgressUpdate, message);
    }

    public void PublishCommandResult(CommandResultMessage result, Guid? correlationId = null)
    {
        SendEnvelope(ProtocolMessageTypes.CommandResult, result, correlationId);
    }

    private void OnEnvelopeReceived(object? sender, ProtocolEnvelope envelope)
    {
        if (envelope.MessageType == ProtocolMessageTypes.HostRegistration)
            return;

        if (envelope.MessageType == ProtocolMessageTypes.CommandRequest)
        {
            _ = Task.Run(async () =>
            {
                var request = ProtocolSerializer.DeserializePayload<CommandRequestMessage>(envelope);
                if (request == null)
                    return;

                if (!string.Equals(request.TargetInstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                    return;

                var handlers = RemoteCommandReceived;
                if (handlers == null)
                    return;

                var invocationList = handlers.GetInvocationList();
                foreach (var invokable in invocationList)
                {
                    var handler = (Func<CommandRequestMessage, Task<CommandResultMessage>>)invokable;
                    var result = await handler(request);
                    PublishCommandResult(result, envelope.MessageId);
                }
            });
        }
    }

    private void RegisterHost()
    {
        var registration = new HostRegistrationMessage(
            _instanceId,
            _hostName,
            typeof(BackupHostCommunicationService).Assembly.GetName().Version?.ToString() ?? "unknown",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [PauseCapability, ResumeCapability, StopCapability, StartCapability]);

        SendEnvelope(ProtocolMessageTypes.HostRegistration, registration);
    }

    private void OnSocketConnected(object? sender, EventArgs e)
    {
        RegisterHost();
    }

    private void PublishSnapshot(BackupJob job, RemoteJobStatus status, string? detail)
    {
        var snapshotMessage = new BackupJobSnapshotMessage(
            _instanceId,
            job.Id,
            job.Name,
            status,
            job.CurrentProgress,
            job.FilesCount,
            job.CurrentFileIndex,
            job.TransferredSize,
            job.TotalSize,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        SendEnvelope(ProtocolMessageTypes.BackupJobSnapshot, snapshotMessage);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            var progress = BackupExecutionProgressSnapshot.FromJob(job);
            PublishProgress(job, progress, status, detail);
        }
    }

    private void SendEnvelope<TPayload>(string messageType, TPayload payload, Guid? correlationId = null)
    {
        if (!_networkLog.IsConnected)
            return;

        var envelope = ProtocolSerializer.CreateEnvelope(messageType, _instanceId, payload, correlationId);
        _networkLog.SendEnvelope(envelope);
    }

    public static RemoteJobStatus ResolveStatus(BackupJob job)
    {
        if (job.WasStopped)
            return RemoteJobStatus.Stopped;

        if (job.IsPaused())
            return RemoteJobStatus.Paused;

        if (job.CurrentProgress >= 100)
            return RemoteJobStatus.Completed;

        if (job.CurrentProgress > 0)
            return RemoteJobStatus.Running;

        return RemoteJobStatus.Idle;
    }

    public string InstanceId => _instanceId;

    private static string BuildHostName()
    {
        var configured = ApplicationConfiguration.Load().EasySaveHostDisplayName;
        return string.IsNullOrWhiteSpace(configured) ? Environment.MachineName : configured.Trim();
    }

    private static string BuildInstanceId()
    {
        var config = ApplicationConfiguration.Load();
        var label = string.IsNullOrWhiteSpace(config.EasySaveHostInstanceLabel)
            ? Environment.ProcessId.ToString()
            : config.EasySaveHostInstanceLabel.Trim();
        return $"{Environment.MachineName}-{label}";
    }
}
