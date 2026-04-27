using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskBlaster.Externals;

namespace TaskBlaster.Tests;

/// <summary>
/// Tests for <see cref="NupkgImporter"/>: nuspec parsing, TFM precedence,
/// destination-folder hygiene, and the failure modes that surface to the
/// user as red issue rows in the validation dialog.
/// </summary>
public sealed class NupkgImporterTests : IDisposable
{
    private readonly string _temp;
    private readonly string _store;
    private readonly string _dllSrc;

    public NupkgImporterTests()
    {
        _temp = ExternalsFixtures.FreshTempFolder("nupkg");
        _store = Path.Combine(_temp, "packages");
        Directory.CreateDirectory(_store);

        // Build one tiny DLL once and re-pack it into the test nupkgs.
        _dllSrc = Path.Combine(_temp, "Marker.dll");
        ExternalsFixtures.BuildDll(_dllSrc, "Marker", new Version(1, 0, 0, 0));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void ReadIdentity_ReturnsIdAndVersionFromNuspec()
    {
        var nupkg = Path.Combine(_temp, "FooPkg.1.2.3.nupkg");
        ExternalsFixtures.BuildNupkg(nupkg, "FooPkg", "1.2.3", new()
        {
            ["net10.0"] = new[] { _dllSrc },
        });

        var id = NupkgImporter.ReadIdentity(nupkg);
        Assert.Equal("FooPkg", id.Id);
        Assert.Equal("1.2.3",  id.Version);
    }

    [Fact]
    public void Import_PicksHighestPriorityTfm()
    {
        // Provide both net8.0 and net10.0; importer must pick net10.0.
        var nupkg = Path.Combine(_temp, "TfmPick.1.0.0.nupkg");
        ExternalsFixtures.BuildNupkg(nupkg, "TfmPick", "1.0.0", new()
        {
            ["net8.0"]        = new[] { _dllSrc },
            ["netstandard2.0"] = new[] { _dllSrc },
            ["net10.0"]       = new[] { _dllSrc },
        });

        var result = NupkgImporter.Import(nupkg, _store);

        Assert.Equal("net10.0", result.ChosenTfm);
        Assert.Single(result.Dlls);
        Assert.True(File.Exists(result.Dlls[0]));
        Assert.Equal(Path.Combine(_store, "TfmPick", "1.0.0"), result.InstallFolder);
    }

    [Fact]
    public void Import_FallsBackToNetStandardWhenNothingNewer()
    {
        var nupkg = Path.Combine(_temp, "Old.1.0.0.nupkg");
        ExternalsFixtures.BuildNupkg(nupkg, "Old", "1.0.0", new()
        {
            ["netstandard2.0"] = new[] { _dllSrc },
        });

        var result = NupkgImporter.Import(nupkg, _store);

        Assert.Equal("netstandard2.0", result.ChosenTfm);
    }

    [Fact]
    public void Import_ThrowsWhenNoCompatibleTfm()
    {
        // Old .NET Framework only — should refuse loudly so the dialog can
        // tell the user "nope".
        var nupkg = Path.Combine(_temp, "OldNet.1.0.0.nupkg");
        ExternalsFixtures.BuildNupkg(nupkg, "OldNet", "1.0.0", new()
        {
            ["net48"] = new[] { _dllSrc },
        });

        var ex = Assert.Throws<NotSupportedException>(() => NupkgImporter.Import(nupkg, _store));
        Assert.Contains("net48", ex.Message);
    }

    [Fact]
    public void Import_ThrowsWhenNuspecMissing()
    {
        // Hand-craft a zip without a .nuspec at the root.
        var nupkg = Path.Combine(_temp, "NoNuspec.nupkg");
        using (var fs = File.Create(nupkg))
        using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
        {
            var e = zip.CreateEntry("lib/net10.0/Marker.dll");
            using var es = e.Open();
            using var src = File.OpenRead(_dllSrc);
            src.CopyTo(es);
        }

        Assert.Throws<InvalidDataException>(() => NupkgImporter.ReadIdentity(nupkg));
    }

    [Fact]
    public void Import_WipesDestinationFolderBeforeReExtracting()
    {
        // First import lays down Marker.dll; second import (with a different
        // payload) must not leave the original behind.
        var first = Path.Combine(_temp, "Wipe.1.0.0.first.nupkg");
        ExternalsFixtures.BuildNupkg(first, "Wipe", "1.0.0", new()
        {
            ["net10.0"] = new[] { _dllSrc },
        });
        NupkgImporter.Import(first, _store);

        var pollutionPath = Path.Combine(_store, "Wipe", "1.0.0", "Pollution.txt");
        File.WriteAllText(pollutionPath, "stale");

        var second = Path.Combine(_temp, "Wipe.1.0.0.second.nupkg");
        var second2 = Path.Combine(_temp, "Other.dll");
        ExternalsFixtures.BuildDll(second2, "Other", new Version(1, 0, 0, 0));
        ExternalsFixtures.BuildNupkg(second, "Wipe", "1.0.0", new()
        {
            ["net10.0"] = new[] { second2 },
        });
        var result = NupkgImporter.Import(second, _store);

        Assert.False(File.Exists(pollutionPath));
        Assert.Equal("Other.dll", Path.GetFileName(result.Dlls.Single()));
        Assert.False(File.Exists(Path.Combine(_store, "Wipe", "1.0.0", "Marker.dll")));
    }
}
