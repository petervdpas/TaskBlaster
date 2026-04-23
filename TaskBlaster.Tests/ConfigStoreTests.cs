using System;
using System.IO;
using TaskBlaster;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _temp;

    public ConfigStoreTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "tb-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void Defaults_PointInsideBaseDirectory()
    {
        IConfigStore cfg = new ConfigStore(_temp);
        Assert.Equal(Path.Combine(_temp, "scripts"), cfg.ScriptsFolder);
        Assert.Equal(Path.Combine(_temp, "forms"),   cfg.FormsFolder);
    }

    [Fact]
    public void Load_MissingFile_KeepsDefaults()
    {
        IConfigStore cfg = new ConfigStore(_temp);
        cfg.Load();
        Assert.Equal(Path.Combine(_temp, "scripts"), cfg.ScriptsFolder);
        Assert.Equal(Path.Combine(_temp, "forms"),   cfg.FormsFolder);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var scripts = Path.Combine(_temp, "custom-scripts");
        var forms   = Path.Combine(_temp, "custom-forms");

        var writer = new ConfigStore(_temp) { ScriptsFolder = scripts, FormsFolder = forms };
        writer.Save();

        var reader = new ConfigStore(_temp);
        reader.Load();

        Assert.Equal(scripts, reader.ScriptsFolder);
        Assert.Equal(forms,   reader.FormsFolder);
    }

    [Fact]
    public void Load_MalformedJson_FallsBackToDefaults()
    {
        File.WriteAllText(Path.Combine(_temp, "config.json"), "this is not json {{");

        IConfigStore cfg = new ConfigStore(_temp);
        cfg.Load();

        Assert.Equal(Path.Combine(_temp, "scripts"), cfg.ScriptsFolder);
    }

    [Fact]
    public void Save_CreatesBaseDirectoryIfMissing()
    {
        var nested = Path.Combine(_temp, "not-yet-created");
        var cfg = new ConfigStore(nested);
        cfg.Save();

        Assert.True(File.Exists(Path.Combine(nested, "config.json")));
    }
}
