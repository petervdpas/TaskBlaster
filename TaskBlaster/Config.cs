using System;
using System.IO;
using System.Text.Json;

namespace TaskBlaster;

/// <summary>
/// Simple JSON-backed app config at ~/.taskblaster/config.json.
/// </summary>
public static class Config
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".taskblaster");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public static readonly string DefaultScriptsFolder = Path.Combine(ConfigDir, "scripts");

    public static string ScriptsFolder { get; set; } = DefaultScriptsFolder;

    public static void Load()
    {
        if (!File.Exists(ConfigPath)) return;
        try
        {
            var data = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(ConfigPath));
            if (data is not null && !string.IsNullOrWhiteSpace(data.ScriptsFolder))
                ScriptsFolder = data.ScriptsFolder;
        }
        catch
        {
            // ignore and keep defaults
        }
    }

    public static void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var data = new ConfigData { ScriptsFolder = ScriptsFolder };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    private sealed class ConfigData
    {
        public string? ScriptsFolder { get; set; }
    }
}
