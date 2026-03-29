using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasySave.Protocol.Messages;
using EasySave.RemoteConsole.Services;

namespace EasySave.RemoteConsole.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRemoteConsoleClientService _clientService;

    [ObservableProperty] private string _serverIp = "127.0.0.1";
    [ObservableProperty] private int _serverPort = 5000;
    [ObservableProperty] private string _connectionState = "Disconnected";
    [ObservableProperty] private string _selectedHostInstanceId = string.Empty;
    [ObservableProperty] private string _feedbackMessage = "Ready.";
    [ObservableProperty] private bool _isConnected;

    public MainWindowViewModel(IRemoteConsoleClientService clientService)
    {
        _clientService = clientService;
        Hosts = new ObservableCollection<HostInstanceItemViewModel>();
        Jobs = new ObservableCollection<RemoteJobItemViewModel>();

        _clientService.ConnectionStateChanged += OnConnectionStateChanged;
        _clientService.HostDiscovered += OnHostDiscovered;
        _clientService.JobSnapshotReceived += OnJobSnapshotReceived;
        _clientService.ProgressReceived += OnProgressReceived;
        _clientService.CommandResultReceived += OnCommandResultReceived;
        _clientService.ErrorReceived += OnErrorReceived;
    }

    public ObservableCollection<HostInstanceItemViewModel> Hosts { get; }
    public ObservableCollection<RemoteJobItemViewModel> Jobs { get; }

    public bool CanConnect => !IsConnected;
    public bool CanDisconnect => IsConnected;
    public bool CanSelectHost => IsConnected;
    public string SelectedHostLabel => string.IsNullOrWhiteSpace(SelectedHostInstanceId)
        ? "No host selected"
        : SelectedHostInstanceId;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            await _clientService.ConnectAsync(ServerIp.Trim(), ServerPort, string.IsNullOrWhiteSpace(SelectedHostInstanceId) ? null : SelectedHostInstanceId.Trim());
            FeedbackMessage = "Connected. Waiting for host announcements.";
        }
        catch (Exception ex)
        {
            FeedbackMessage = $"Connection failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _clientService.DisconnectAsync();
        FeedbackMessage = "Disconnected from server.";
    }

    [RelayCommand]
    private async Task SelectHostAsync()
    {
        if (!IsConnected)
            return;

        await _clientService.SelectHostAsync(string.IsNullOrWhiteSpace(SelectedHostInstanceId) ? null : SelectedHostInstanceId.Trim());

        Jobs.Clear();
        FeedbackMessage = string.IsNullOrWhiteSpace(SelectedHostInstanceId)
            ? "Host subscription cleared."
            : $"Subscribed to host '{SelectedHostInstanceId.Trim()}'.";
        OnPropertyChanged(nameof(SelectedHostLabel));
    }

    [RelayCommand]
    private void UseHost(HostInstanceItemViewModel? host)
    {
        if (host is null)
            return;

        SelectedHostInstanceId = host.InstanceId;
        _ = SelectHostAsync();
    }

    [RelayCommand]
    private async Task PauseJobAsync(RemoteJobItemViewModel? job)
    {
        await SendJobCommandAsync(job, CommandType.Pause);
    }

    [RelayCommand]
    private async Task ResumeJobAsync(RemoteJobItemViewModel? job)
    {
        await SendJobCommandAsync(job, CommandType.Resume);
    }

    [RelayCommand]
    private async Task StopJobAsync(RemoteJobItemViewModel? job)
    {
        await SendJobCommandAsync(job, CommandType.Stop);
    }

    private async Task SendJobCommandAsync(RemoteJobItemViewModel? job, CommandType command)
    {
        if (job is null || !IsConnected || string.IsNullOrWhiteSpace(SelectedHostInstanceId))
            return;

        try
        {
            await _clientService.SendCommandAsync(SelectedHostInstanceId.Trim(), job.JobId, job.JobName, command, Environment.UserName);
            FeedbackMessage = $"Command {command} sent to '{job.JobName}'.";
        }
        catch (Exception ex)
        {
            FeedbackMessage = $"Command failed: {ex.Message}";
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
            ConnectionState = connected ? "Connected" : "Disconnected";
            if (!connected)
                FeedbackMessage = "Connection lost. Auto-reconnect is attempting...";

            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
            OnPropertyChanged(nameof(CanSelectHost));
        });
    }

    private void OnHostDiscovered(object? sender, HostRegistrationMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = Hosts.FirstOrDefault(x => x.InstanceId.Equals(message.InstanceId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                Hosts.Add(new HostInstanceItemViewModel(message));
            }
            else
            {
                existing.Update(message);
            }

            FeedbackMessage = $"Host available: {message.HostName} ({message.InstanceId})";
        });
    }

    private void OnJobSnapshotReceived(object? sender, BackupJobSnapshotMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpsertHostFromInstance(message.InstanceId);
            if (!IsForCurrentHost(message.InstanceId))
                return;

            var job = FindOrCreateJob(message.JobId, message.JobName);
            job.ApplySnapshot(message);
        });
    }

    private void OnProgressReceived(object? sender, ProgressUpdateMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpsertHostFromInstance(message.InstanceId);
            if (!IsForCurrentHost(message.InstanceId))
                return;

            var job = FindOrCreateJob(message.JobId, message.JobName);
            job.ApplyProgress(message);
        });
    }

    private void OnCommandResultReceived(object? sender, CommandResultMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsForCurrentHost(message.InstanceId))
                return;

            var job = FindOrCreateJob(message.JobId, message.JobName);
            job.ApplyCommandResult(message);
            FeedbackMessage = $"{message.Command} for '{message.JobName}': {message.Status}.";
        });
    }

    private void OnErrorReceived(object? sender, ErrorMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            FeedbackMessage = $"{message.ErrorCode}: {message.Error}";
            if (string.Equals(message.ErrorCode, "HOST_DISCONNECTED", StringComparison.OrdinalIgnoreCase))
            {
                Jobs.Clear();
            }
        });
    }

    private bool IsForCurrentHost(string instanceId)
    {
        return !string.IsNullOrWhiteSpace(SelectedHostInstanceId)
               && instanceId.Equals(SelectedHostInstanceId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private RemoteJobItemViewModel FindOrCreateJob(int jobId, string jobName)
    {
        var existing = Jobs.FirstOrDefault(j => j.JobId == jobId);
        if (existing is not null)
            return existing;

        var created = new RemoteJobItemViewModel(jobId, jobName);
        Jobs.Add(created);
        return created;
    }

    private void UpsertHostFromInstance(string instanceId)
    {
        if (Hosts.Any(h => h.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase)))
            return;

        Hosts.Add(new HostInstanceItemViewModel(new HostRegistrationMessage(
            instanceId,
            instanceId,
            "unknown",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null)));
    }

    partial void OnSelectedHostInstanceIdChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedHostLabel));
    }
}
