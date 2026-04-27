using System;
using System.IO;
using TaskBlaster;
using TaskBlaster.Externals;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

/// <summary>
/// Tests covering the new <see cref="IConfigStore.ExternalDlls"/> and
/// <see cref="IConfigStore.ExternalPackages"/> properties: round-trip
/// through Save/Load and tolerance of pre-External legacy configs.
/// </summary>
public sealed class ConfigStoreExternalsTests : IDisposable
{
    private readonly string _temp;

    public ConfigStoreExternalsTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "tb-extcfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        IConfigStore cfg = new ConfigStore(_temp);
        Assert.Empty(cfg.ExternalDlls);
        Assert.Empty(cfg.ExternalPackages);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsExternalEntries()
    {
        var writer = new ConfigStore(_temp);
        writer.ExternalDlls.Add("/path/to/foo.dll");
        writer.ExternalDlls.Add("/path/to/bar.dll");
        writer.ExternalPackages.Add(new ExternalPackageRef("Acme.Domain", "1.0.0"));
        writer.ExternalPackages.Add(new ExternalPackageRef("Other.Lib",   "2.3.1"));
        writer.Save();

        var reader = new ConfigStore(_temp);
        reader.Load();

        Assert.Equal(2, reader.ExternalDlls.Count);
        Assert.Contains("/path/to/foo.dll", reader.ExternalDlls);
        Assert.Contains("/path/to/bar.dll", reader.ExternalDlls);

        Assert.Equal(2, reader.ExternalPackages.Count);
        Assert.Contains(new ExternalPackageRef("Acme.Domain", "1.0.0"), reader.ExternalPackages);
        Assert.Contains(new ExternalPackageRef("Other.Lib",   "2.3.1"), reader.ExternalPackages);
    }

    [Fact]
    public void Load_LegacyConfigWithoutExternalFields_LeavesEmptyLists()
    {
        // Pre-External config files only had folders + theme. Loading must
        // not crash and must leave both External lists empty.
        File.WriteAllText(Path.Combine(_temp, "config.json"),
            "{ \"Theme\": \"Industrial\", \"TerminalVisible\": true }");

        IConfigStore cfg = new ConfigStore(_temp);
        cfg.Load();

        Assert.Empty(cfg.ExternalDlls);
        Assert.Empty(cfg.ExternalPackages);
    }

    [Fact]
    public void Load_DropsMalformedPackageEntries()
    {
        // Defensive: a hand-edited config.json with a package missing its
        // version (or id) shouldn't poison the whole load. The malformed
        // entry is silently dropped; valid ones survive.
        File.WriteAllText(Path.Combine(_temp, "config.json"), """
            {
              "ExternalPackages": [
                { "Id": "Good.Pkg", "Version": "1.0.0" },
                { "Id": "Missing.Version" },
                { "Version": "2.0.0" }
              ]
            }
            """);

        IConfigStore cfg = new ConfigStore(_temp);
        cfg.Load();

        Assert.Single(cfg.ExternalPackages);
        Assert.Equal("Good.Pkg", cfg.ExternalPackages[0].Id);
        Assert.Equal("1.0.0",    cfg.ExternalPackages[0].Version);
    }
}
