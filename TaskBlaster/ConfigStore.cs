using System;
using System.IO;
using System.Text.Json;
using TaskBlaster.Interfaces;

namespace TaskBlaster;

/// <summary>
/// JSON-backed IConfigStore. Default location is ~/.taskblaster/config.json;
/// tests pass a custom base directory via the constructor.
/// </summary>
public sealed class ConfigStore : IConfigStore
{
    private readonly string _baseDirectory;

    public ConfigStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".taskblaster"))
    { }

    public ConfigStore(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        ScriptsFolder   = Path.Combine(_baseDirectory, "scripts");
        FormsFolder     = Path.Combine(_baseDirectory, "forms");
        VaultFolder     = Path.Combine(_baseDirectory, "vault");
        Theme           = "Industrial";
        TerminalVisible = true;
    }

    public string ScriptsFolder   { get; set; }
    public string FormsFolder     { get; set; }
    public string VaultFolder     { get; set; }
    public string Theme           { get; set; }
    public bool   TerminalVisible { get; set; }

    private string ConfigPath => Path.Combine(_baseDirectory, "config.json");

    public void Load()
    {
        if (!File.Exists(ConfigPath)) return;
        try
        {
            var data = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(ConfigPath));
            if (data is null) return;
            if (!string.IsNullOrWhiteSpace(data.ScriptsFolder)) ScriptsFolder = data.ScriptsFolder;
            if (!string.IsNullOrWhiteSpace(data.FormsFolder))   FormsFolder   = data.FormsFolder;
            if (!string.IsNullOrWhiteSpace(data.VaultFolder))   VaultFolder   = data.VaultFolder;
            if (!string.IsNullOrWhiteSpace(data.Theme))         Theme         = data.Theme;
            if (data.TerminalVisible.HasValue)                  TerminalVisible = data.TerminalVisible.Value;
        }
        catch
        {
            // ignore malformed config and keep defaults
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(_baseDirectory);
        var data = new ConfigData
        {
            ScriptsFolder   = ScriptsFolder,
            FormsFolder     = FormsFolder,
            VaultFolder     = VaultFolder,
            Theme           = Theme,
            TerminalVisible = TerminalVisible,
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    private sealed class ConfigData
    {
        public string? ScriptsFolder   { get; set; }
        public string? FormsFolder     { get; set; }
        public string? VaultFolder     { get; set; }
        public string? Theme           { get; set; }
        public bool?   TerminalVisible { get; set; }
    }
}
