using System.Text.Json;

namespace EasySave.RemoteConsole.Services;

public sealed class RemoteConsoleConfiguration
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static RemoteConsoleConfiguration? _instance;

    public string ServerIp { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 5000;
    public string SelectedHostInstanceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = Environment.MachineName;

    public static RemoteConsoleConfiguration Load(string fileName = "remoteconsole.settings.json")
    {
        lock (Sync)
        {
            if (_instance != null)
                return _instance;

            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path))
            {
                _instance = new RemoteConsoleConfiguration();
                Save(path, _instance);
                return _instance;
            }

            var json = File.ReadAllText(path);
            _instance = JsonSerializer.Deserialize<RemoteConsoleConfiguration>(json) ?? new RemoteConsoleConfiguration();
            _instance.ServerPort = _instance.ServerPort <= 0 ? 5000 : _instance.ServerPort;
            _instance.ServerIp = string.IsNullOrWhiteSpace(_instance.ServerIp) ? "127.0.0.1" : _instance.ServerIp.Trim();
            _instance.DisplayName = string.IsNullOrWhiteSpace(_instance.DisplayName) ? Environment.MachineName : _instance.DisplayName.Trim();
            return _instance;
        }
    }

    public void Save(string fileName = "remoteconsole.settings.json")
    {
        lock (Sync)
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            Save(path, this);
        }
    }

    private static void Save(string path, RemoteConsoleConfiguration configuration)
    {
        var json = JsonSerializer.Serialize(configuration, JsonOptions);
        File.WriteAllText(path, json);
    }
}
