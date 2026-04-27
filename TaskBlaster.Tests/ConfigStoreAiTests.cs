using System;
using System.IO;
using TaskBlaster;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

/// <summary>
/// Tests covering the <see cref="IConfigStore.AiDefaultProvider"/>
/// field: defaults to null (AI disabled), round-trips through
/// Save/Load, legacy configs without the field load cleanly.
/// </summary>
public sealed class ConfigStoreAiTests : IDisposable
{
    private readonly string _temp;

    public ConfigStoreAiTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "tb-aicfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void Default_AiDefaultProviderIsNull()
    {
        IConfigStore cfg = new ConfigStore(_temp);
        Assert.Null(cfg.AiDefaultProvider);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAiProviderName()
    {
        var writer = new ConfigStore(_temp) { AiDefaultProvider = "anthropic" };
        writer.Save();

        var reader = new ConfigStore(_temp);
        reader.Load();

        Assert.Equal("anthropic", reader.AiDefaultProvider);
    }

    [Fact]
    public void Save_ThenLoad_PersistsNullProviderAsAbsence()
    {
        // A null provider doesn't get serialized as "AiDefaultProvider": null
        // in a way the loader has to handle separately — the loader's
        // null-or-whitespace guard means an absent / null / empty value
        // all collapse to "leave defaults alone", which is null here.
        var writer = new ConfigStore(_temp) { AiDefaultProvider = null };
        writer.Save();

        var reader = new ConfigStore(_temp);
        reader.Load();

        Assert.Null(reader.AiDefaultProvider);
    }

    [Fact]
    public void Load_LegacyConfigWithoutAiField_LeavesProviderNull()
    {
        // Pre-AI config files don't carry the field at all; loader must
        // not crash and the default (null = disabled) must stick.
        File.WriteAllText(Path.Combine(_temp, "config.json"),
            "{ \"Theme\": \"Industrial\", \"TerminalVisible\": true }");

        IConfigStore cfg = new ConfigStore(_temp);
        cfg.Load();

        Assert.Null(cfg.AiDefaultProvider);
    }

    [Fact]
    public void Load_IgnoresWhitespaceProvider()
    {
        // Hand-edited config with an all-whitespace provider name should
        // not register as a real provider — same defensive treatment we
        // give other string fields.
        File.WriteAllText(Path.Combine(_temp, "config.json"),
            "{ \"AiDefaultProvider\": \"   \" }");

        IConfigStore cfg = new ConfigStore(_temp);
        cfg.Load();

        Assert.Null(cfg.AiDefaultProvider);
    }
}
