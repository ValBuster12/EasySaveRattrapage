using EasySave.Protocol.Messages;

namespace EasySave.RemoteConsole.ViewModels;

public partial class HostInstanceItemViewModel : ViewModelBase
{
    public HostInstanceItemViewModel(HostRegistrationMessage message)
    {
        Update(message);
    }

    public string InstanceId { get; private set; } = string.Empty;
    public string HostName { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; private set; }

    public string DisplayName => $"{HostName} ({InstanceId})";

    public void Update(HostRegistrationMessage message)
    {
        InstanceId = message.InstanceId;
        HostName = message.HostName;
        Version = message.ApplicationVersion;
        StartedAtUtc = message.StartedAtUtc;

        OnPropertyChanged(nameof(InstanceId));
        OnPropertyChanged(nameof(HostName));
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(StartedAtUtc));
        OnPropertyChanged(nameof(DisplayName));
    }
}
