using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EasySave.RemoteConsole.Services;
using EasySave.RemoteConsole.ViewModels;
using EasySave.RemoteConsole.Views;

namespace EasySave.RemoteConsole;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var service = new RemoteConsoleClientService();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(service)
            };

            desktop.Exit += async (_, _) => await service.DisposeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
