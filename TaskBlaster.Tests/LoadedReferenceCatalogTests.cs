using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaskBlaster.Engine;
using TaskBlaster.Externals;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

/// <summary>
/// Exercises <see cref="LoadedReferenceCatalog"/> against the live test
/// AppDomain. The test process loads dozens of assemblies (BCL, xunit,
/// TaskBlaster + its Blast deps), which gives every classification path
/// real coverage without needing fixtures.
/// </summary>
public sealed class LoadedReferenceCatalogTests : IDisposable
{
    private readonly string _temp;
    private readonly StubConfigStore _config;
    private readonly ExternalReferenceManager _externals;
    private readonly LoadedReferenceCatalog _catalog;

    public LoadedReferenceCatalogTests()
    {
        _temp      = ExternalsFixtures.FreshTempFolder("catalog");
        _config    = new StubConfigStore();
        _externals = new ExternalReferenceManager(_config, Path.Combine(_temp, "packages"));
        _catalog   = new LoadedReferenceCatalog(_config, _externals);
    }

    public void Dispose()
    {
        // Same reason as ExternalReferenceManagerTests: we may have called
        // Assembly.LoadFrom on files in _temp; deleting the folder would
        // poison every later AppDomain.GetAssemblies() walk.
    }

    [Fact]
    public void Snapshot_IsNonEmpty_AndAllEntriesPointAtRealFiles()
    {
        var snapshot = _catalog.Snapshot();

        Assert.NotEmpty(snapshot);
        // Every Location must be a real file — the catalog filters ghosts
        // the same way ScriptBlaster.GetLoadableAssemblies does, so the
        // AI assistant downstream can trust it.
        Assert.All(snapshot, r => Assert.True(File.Exists(r.Location), $"Missing: {r.Location}"));
    }

    [Fact]
    public void Snapshot_ClassifiesRuntimeBclAsFramework()
    {
        // System.Runtime is loaded into every .NET process; it lives in
        // RuntimeEnvironment.GetRuntimeDirectory().
        var snapshot = _catalog.Snapshot();

        var systemRuntime = snapshot.FirstOrDefault(r =>
            string.Equals(r.Name, "System.Runtime", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(systemRuntime);
        Assert.Equal(LoadedReferenceOrigin.Framework, systemRuntime!.Origin);
    }

    [Fact]
    public void Snapshot_ClassifiesBlastNugetsByPrimaryFacadeAttribute()
    {
        // TaskBlaster.csproj references the Blast nugets, so they're in
        // AppDomain. The catalog should pick out everything carrying the
        // Blast.PrimaryFacade attribute, regardless of where on disk the
        // file lives.
        var snapshot = _catalog.Snapshot();
        var blast = snapshot.Where(r => r.Origin == LoadedReferenceOrigin.Blast).ToList();

        Assert.NotEmpty(blast);
        // Every Blast entry must surface at least one facade type — the
        // attribute exists by construction.
        Assert.All(blast, r => Assert.NotEmpty(r.PrimaryFacades));
    }

    [Fact]
    public void Snapshot_SurfacesUtilBlastFrontDoorTypes()
    {
        // TaskBlaster references UtilBlast 1.2.1+, which carries the
        // attribute we just added. The catalog should expose the two
        // declared facade types.
        var snapshot = _catalog.Snapshot();
        var utilBlast = snapshot.FirstOrDefault(r =>
            string.Equals(r.Name, "UtilBlast", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(utilBlast);
        Assert.Equal(LoadedReferenceOrigin.Blast, utilBlast!.Origin);
        Assert.Contains("UtilBlast.Tabular.Blast",   utilBlast.PrimaryFacades);
        Assert.Contains("UtilBlast.UtilBlastFactory", utilBlast.PrimaryFacades);
    }

    [Fact]
    public void Snapshot_ClassifiesLoadedExternalDllAsExternal()
    {
        // Build a DLL, register it in the stub config's ExternalDlls list,
        // load it into the AppDomain, then snapshot — must come back as
        // External even though it lives in our temp folder, not under any
        // package store.
        var dll = Path.Combine(_temp, $"ExtFixture_{Guid.NewGuid():N}.dll");
        ExternalsFixtures.BuildDll(dll, $"ExtFixture_{Guid.NewGuid():N}", new Version(1, 0, 0, 0));
        _config.ExternalDlls.Add(dll);
        Assembly.LoadFrom(dll);

        var snapshot = _catalog.Snapshot();
        var match = snapshot.FirstOrDefault(r =>
            string.Equals(r.Location, dll, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(match);
        Assert.Equal(LoadedReferenceOrigin.External, match!.Origin);
        Assert.Empty(match.PrimaryFacades); // synthetic DLL has no Blast attribute
    }

    [Fact]
    public void SnapshotByOrigin_FiltersToRequestedKindsOnly()
    {
        var blastOnly = _catalog.SnapshotByOrigin(LoadedReferenceOrigin.Blast);
        Assert.NotEmpty(blastOnly);
        Assert.All(blastOnly, r => Assert.Equal(LoadedReferenceOrigin.Blast, r.Origin));

        // Empty origins array means "no filter" → returns the full snapshot.
        var noFilter = _catalog.SnapshotByOrigin(Array.Empty<LoadedReferenceOrigin>());
        Assert.True(noFilter.Count >= blastOnly.Count);
    }

    [Fact]
    public void ReadPrimaryFacades_ParsesCommaSeparatedTrimmedNames()
    {
        // Static helper, exercised against the real UtilBlast assembly.
        var utilBlastAsm = typeof(UtilBlast.UtilBlastFactory).Assembly;

        var facades = LoadedReferenceCatalog.ReadPrimaryFacades(utilBlastAsm);

        Assert.Contains("UtilBlast.Tabular.Blast",   facades);
        Assert.Contains("UtilBlast.UtilBlastFactory", facades);
        Assert.All(facades, f => Assert.Equal(f, f.Trim())); // no stray whitespace
    }

    [Fact]
    public void ReadPrimaryFacades_ReturnsEmptyForAssemblyWithoutAttribute()
    {
        // The test assembly itself has no Blast.PrimaryFacade attribute.
        var facades = LoadedReferenceCatalog.ReadPrimaryFacades(typeof(LoadedReferenceCatalogTests).Assembly);
        Assert.Empty(facades);
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
        public string? AiDefaultProvider { get; set; }
        public void Load() { }
        public void Save() { }
    }
}
