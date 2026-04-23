using System;
using System.IO;
using System.Text.Json;

namespace TaskBlaster;

/// <summary>
/// Simple JSON-backed app config at &lt;BaseDirectory&gt;/config.json.
/// By default BaseDirectory is ~/.taskblaster; tests may override it.
/// </summary>
public static class Config
{
    private static readonly string DefaultBaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".taskblaster");

    /// <summary>Directory that holds config.json. Tests may override.</summary>
    public static string BaseDirectory { get; set; } = DefaultBaseDir;

    public static string DefaultScriptsFolder => Path.Combine(BaseDirectory, "scripts");
    public static string DefaultFormsFolder   => Path.Combine(BaseDirectory, "forms");

    public static string ScriptsFolder { get; set; } = Path.Combine(DefaultBaseDir, "scripts");
    public static string FormsFolder   { get; set; } = Path.Combine(DefaultBaseDir, "forms");

    private static string ConfigPath => Path.Combine(BaseDirectory, "config.json");

    public static void Load()
    {
        if (!File.Exists(ConfigPath)) return;
        try
        {
            var data = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(ConfigPath));
            if (data is null) return;
            if (!string.IsNullOrWhiteSpace(data.ScriptsFolder)) ScriptsFolder = data.ScriptsFolder;
            if (!string.IsNullOrWhiteSpace(data.FormsFolder))   FormsFolder   = data.FormsFolder;
        }
        catch
        {
            // ignore and keep defaults
        }
    }

    public static void Save()
    {
        Directory.CreateDirectory(BaseDirectory);
        var data = new ConfigData { ScriptsFolder = ScriptsFolder, FormsFolder = FormsFolder };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    private sealed class ConfigData
    {
        public string? ScriptsFolder { get; set; }
        public string? FormsFolder   { get; set; }
    }
}
