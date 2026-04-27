using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskBlaster.Externals;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

/// <summary>
/// End-to-end behaviour of <see cref="ExternalReferenceManager"/>: add /
/// remove / persist round-trip, force-flag override, and the load-all
/// startup path that swallows per-entry failures into an error list
/// rather than throwing.
/// </summary>
public sealed class ExternalReferenceManagerTests : IDisposable
{
    private readonly string _temp;
    private readonly string _packageStore;
    private readonly StubConfigStore _config;
    private readonly ExternalReferenceManager _manager;

    public ExternalReferenceManagerTests()
    {
        _temp = ExternalsFixtures.FreshTempFolder("mgr");
        _packageStore = Path.Combine(_temp, "packages");
        Directory.CreateDirectory(_packageStore);

        _config  = new StubConfigStore();
        _manager = new ExternalReferenceManager(_config, _packageStore);
    }

    public void Dispose()
    {
        // We deliberately leak the temp folder: AddDll calls Assembly.LoadFrom,
        // and the resulting Assembly objects keep their original Location path
        // for the lifetime of the AppDomain. Deleting the file would poison
        // every later test that builds a Roslyn reference list off
        // AppDomain.GetAssemblies(). The OS prunes /tmp; we just don't fight it.
    }

    [Fact]
    public void AddDll_WithCleanAssembly_PersistsAndCountsAsLoaded()
    {
        var dll = Path.Combine(_temp, $"Mgr_{Guid.NewGuid():N}.dll");
        ExternalsFixtures.BuildDll(dll, $"Mgr_{Guid.NewGuid():N}", new Version(1, 0, 0, 0));

        var outcome = _manager.AddDll(dll, force: false);

        Assert.True(outcome.Loaded);
        Assert.Single(outcome.LoadedDlls);
        Assert.Contains(dll, _config.ExternalDlls);
        Assert.True(_config.SaveCalls > 0);
    }

    [Fact]
    public void AddDll_WhenFileMissing_DoesNotPersistAndReportsError()
    {
        var outcome = _manager.AddDll(Path.Combine(_temp, "ghost.dll"), force: false);

        Assert.False(outcome.Loaded);
        Assert.Empty(_config.ExternalDlls);
        Assert.NotEmpty(outcome.RuntimeErrors);
        Assert.Contains("not found", outcome.RuntimeErrors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddDll_WithConflict_ForceFalse_DoesNotPersist()
    {
        // Pre-load a v1 of "Twin"; second AddDll for v2 must be blocked
        // unless the user explicitly forces.
        var v1 = Path.Combine(_temp, "TwinV1.dll");
        ExternalsFixtures.BuildDll(v1, "TwinM", new Version(1, 0, 0, 0));
        Assert.True(_manager.AddDll(v1, force: false).Loaded);
        var v1Snapshot = _config.SaveCalls;

        var v2Folder = Path.Combine(_temp, "v2");
        Directory.CreateDirectory(v2Folder);
        var v2 = Path.Combine(v2Folder, "TwinV2.dll");
        ExternalsFixtures.BuildDll(v2, "TwinM", new Version(2, 0, 0, 0));

        var outcome = _manager.AddDll(v2, force: false);

        Assert.False(outcome.Loaded);
        Assert.DoesNotContain(v2, _config.ExternalDlls);
        Assert.Equal(v1Snapshot, _config.SaveCalls); // no extra save
        // The report must surface the conflict so the dialog can render it.
        Assert.Contains(outcome.Reports.Single().Issues, i => i.Level == IssueLevel.Error);
    }

    [Fact]
    public void AddPackage_ExtractsAndPersistsIdentity()
    {
        var dllSrc = Path.Combine(_temp, "Embed.dll");
        ExternalsFixtures.BuildDll(dllSrc, "Embed", new Version(1, 0, 0, 0));
        var nupkg = Path.Combine(_temp, "PkgA.1.0.0.nupkg");
        ExternalsFixtures.BuildNupkg(nupkg, "PkgA", "1.0.0", new()
        {
            ["net10.0"] = new[] { dllSrc },
        });

        var outcome = _manager.AddPackage(nupkg, force: false);

        Assert.True(outcome.Loaded);
        Assert.Single(_config.ExternalPackages);
        Assert.Equal("PkgA",  _config.ExternalPackages[0].Id);
        Assert.Equal("1.0.0", _config.ExternalPackages[0].Version);
        Assert.True(File.Exists(Path.Combine(_packageStore, "PkgA", "1.0.0", "Embed.dll")));
    }

    [Fact]
    public void AddPackage_SameIdDifferentVersion_ReplacesEntry()
    {
        // The manager treats packages as id-unique: importing a new
        // version of an already-imported id replaces the entry rather
        // than accumulating duplicates.
        var dllV1 = Path.Combine(_temp, "Replace.v1.dll");
        ExternalsFixtures.BuildDll(dllV1, "Replace", new Version(1, 0, 0, 0));
        var nupkgV1 = Path.Combine(_temp, "Replace.1.0.0.nupkg");
        ExternalsFixtures.BuildNupkg(nupkgV1, "Replace", "1.0.0", new()
        {
            ["net10.0"] = new[] { dllV1 },
        });
        Assert.True(_manager.AddPackage(nupkgV1, force: false).Loaded);

        var dllV2Folder = Path.Combine(_temp, "v2");
        Directory.CreateDirectory(dllV2Folder);
        var dllV2 = Path.Combine(dllV2Folder, "Replace.v2.dll");
        ExternalsFixtures.BuildDll(dllV2, "Replace", new Version(2, 0, 0, 0));
        var nupkgV2 = Path.Combine(_temp, "Replace.2.0.0.nupkg");
        ExternalsFixtures.BuildNupkg(nupkgV2, "Replace", "2.0.0", new()
        {
            ["net10.0"] = new[] { dllV2 },
        });
        // Same package id with a NEW version → user clicks "Add anyway".
        // Live load can't succeed in the same process (the default
        // AssemblyLoadContext refuses two same-simple-name assemblies)
        // so upgrade.Loaded will be false — but the config entry must
        // still be replaced so the next launch loads v2 cleanly.
        var upgrade = _manager.AddPackage(nupkgV2, force: true);

        Assert.False(upgrade.Loaded);             // expected: live upgrade impossible
        Assert.NotEmpty(upgrade.RuntimeErrors);    // and the load error is reported
        Assert.Single(_config.ExternalPackages);
        Assert.Equal("2.0.0", _config.ExternalPackages[0].Version);
    }

    [Fact]
    public void RemovePackage_DeletesFolderAndConfigEntry()
    {
        var dll = Path.Combine(_temp, "RemPkg.dll");
        ExternalsFixtures.BuildDll(dll, "RemPkg", new Version(1, 0, 0, 0));
        var nupkg = Path.Combine(_temp, "RemPkg.1.0.0.nupkg");
        ExternalsFixtures.BuildNupkg(nupkg, "RemPkg", "1.0.0", new()
        {
            ["net10.0"] = new[] { dll },
        });
        _manager.AddPackage(nupkg, force: false);

        var folder = Path.Combine(_packageStore, "RemPkg", "1.0.0");
        Assert.True(Directory.Exists(folder));

        _manager.RemovePackage(new ExternalPackageRef("RemPkg", "1.0.0"));

        Assert.Empty(_config.ExternalPackages);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void RemoveDll_DropsConfigEntry()
    {
        var dll = Path.Combine(_temp, "RemDll.dll");
        ExternalsFixtures.BuildDll(dll, "RemDll", new Version(1, 0, 0, 0));
        _manager.AddDll(dll, force: false);

        Assert.Contains(dll, _config.ExternalDlls);

        _manager.RemoveDll(dll);

        Assert.DoesNotContain(dll, _config.ExternalDlls);
    }

    [Fact]
    public void LoadAll_CollectsErrorsForMissingEntriesWithoutThrowing()
    {
        _config.ExternalDlls.Add(Path.Combine(_temp, "missing.dll"));
        _config.ExternalPackages.Add(new ExternalPackageRef("NoSuchPkg", "9.9.9"));

        var outcome = _manager.LoadAll();

        Assert.Equal(0, outcome.LoadedDllCount);
        Assert.Equal(2, outcome.Errors.Count);
        Assert.Contains(outcome.Errors, m => m.Contains("missing.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(outcome.Errors, m => m.Contains("NoSuchPkg",   StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubConfigStore : IConfigStore
    {
        public string ScriptsFolder { get; set; } = "";
        public string FormsFolder   { get; set; } = "";
        public string VaultFolder   { get; set; } = "";
        public string Theme         { get; set; } = "Industrial";
        public bool   TerminalVisible   { get; set; } = true;
        public string EditorHighlighter { get; set; } = "Native";
        public bool   CodeFolding       { get; set; } = true;
        public IList<string> ExternalDlls           { get; } = new List<string>();
        public IList<ExternalPackageRef> ExternalPackages { get; } = new List<ExternalPackageRef>();

        public int SaveCalls { get; private set; }
        public void Load() { }
        public void Save() => SaveCalls++;
    }
}
