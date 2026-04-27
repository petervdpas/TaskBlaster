using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Externals;

/// <summary>
/// Result of asking the manager to add a new DLL or package. <see cref="Loaded"/>
/// is the list of DLL paths that were successfully <c>Assembly.LoadFrom</c>'d
/// (empty if nothing was loaded). <see cref="Reports"/> is one entry per
/// candidate DLL with the issues we found, so the dialog can render a
/// per-DLL list with severities.
/// </summary>
public sealed record ExternalAddOutcome(
    bool Loaded,
    IReadOnlyList<string> LoadedDlls,
    IReadOnlyList<AssemblyValidationReport> Reports,
    IReadOnlyList<string> RuntimeErrors);

/// <summary>
/// Result of <see cref="ExternalReferenceManager.LoadAll"/>: how many
/// DLLs we managed to surface to Roslyn, plus the errors we collected
/// for entries that failed (so the terminal can show them at startup).
/// </summary>
public sealed record ExternalStartupOutcome(
    int LoadedDllCount,
    IReadOnlyList<string> Errors);

/// <summary>
/// Owns TaskBlaster's external-reference store. Responsible for:
/// <list type="bullet">
///   <item>Loading the persisted DLL paths and NuGet packages at startup so
///         <c>ScriptBlaster.GetLoadableAssemblies</c> can hand them to
///         Roslyn.</item>
///   <item>Importing fresh <c>.nupkg</c> files into the store, validating
///         them, and live-loading on success.</item>
///   <item>Removing entries from the persisted config (live unload is not
///         possible in the default AppDomain, so removal only takes effect
///         on the next launch — the dialog tells the user this).</item>
/// </list>
/// </summary>
public sealed class ExternalReferenceManager
{
    private readonly IConfigStore _config;
    private readonly string _packageStoreRoot;
    private readonly List<string> _loadedDlls = new();

    public ExternalReferenceManager(IConfigStore config, string packageStoreRoot)
    {
        _config = config;
        _packageStoreRoot = packageStoreRoot;
    }

    /// <summary>Folder under which imported NuGet packages live, one subfolder per id/version.</summary>
    public string PackageStoreRoot => _packageStoreRoot;

    /// <summary>Snapshot of every external DLL currently loaded in this session.</summary>
    public IReadOnlyList<string> LoadedDlls => _loadedDlls;

    /// <summary>The persisted list of NuGet packages (id + version pairs).</summary>
    public IReadOnlyList<ExternalPackageRef> Packages => _config.ExternalPackages.ToList();

    /// <summary>The persisted list of loose DLL paths.</summary>
    public IReadOnlyList<string> Dlls => _config.ExternalDlls.ToList();

    /// <summary>
    /// Walk the persisted config and load every DLL we find. Anything that
    /// fails (missing file, bad metadata, reflection load error) is gathered
    /// into <see cref="ExternalStartupOutcome.Errors"/> rather than thrown,
    /// so a single broken entry doesn't keep the whole app from starting.
    /// </summary>
    public ExternalStartupOutcome LoadAll()
    {
        var errors = new List<string>();

        foreach (var dll in _config.ExternalDlls.ToList())
        {
            if (!File.Exists(dll))
            {
                errors.Add($"External DLL missing: {dll}");
                continue;
            }
            if (!TryLoad(dll, out var err)) errors.Add(err!);
        }

        foreach (var pkg in _config.ExternalPackages.ToList())
        {
            var folder = Path.Combine(_packageStoreRoot, pkg.Id, pkg.Version);
            if (!Directory.Exists(folder))
            {
                errors.Add($"External package missing on disk: {pkg.Id} {pkg.Version} (expected at {folder})");
                continue;
            }
            foreach (var dll in Directory.EnumerateFiles(folder, "*.dll"))
            {
                if (!TryLoad(dll, out var err)) errors.Add(err!);
            }
        }

        return new ExternalStartupOutcome(_loadedDlls.Count, errors);
    }

    /// <summary>
    /// Add a loose DLL by path. Validates first; only loads + persists if
    /// the report has no errors, unless <paramref name="force"/> is true.
    /// </summary>
    public ExternalAddOutcome AddDll(string dllPath, bool force)
    {
        if (!File.Exists(dllPath))
            return Failure($"File not found: {dllPath}");

        var report = AssemblyValidator.Inspect(dllPath, _loadedDlls);
        return CommitDll(dllPath, new[] { report }, force, persist: () =>
        {
            if (!_config.ExternalDlls.Contains(dllPath))
                _config.ExternalDlls.Add(dllPath);
        });
    }

    /// <summary>
    /// Import a <c>.nupkg</c>: extract its <c>lib/&lt;best-tfm&gt;/*.dll</c>
    /// into the package store, validate every extracted DLL, and live-load
    /// them if validation passes (or <paramref name="force"/> is true).
    /// </summary>
    public ExternalAddOutcome AddPackage(string nupkgPath, bool force)
    {
        if (!File.Exists(nupkgPath))
            return Failure($"File not found: {nupkgPath}");

        NupkgImportResult import;
        try { import = NupkgImporter.Import(nupkgPath, _packageStoreRoot); }
        catch (Exception ex) { return Failure($"Could not import {Path.GetFileName(nupkgPath)}: {ex.Message}"); }

        if (import.Dlls.Count == 0)
            return Failure($"Package {import.Package.Id} has no DLLs in lib/{import.ChosenTfm}/.");

        var reports = import.Dlls
            .Select(d => AssemblyValidator.Inspect(d, _loadedDlls))
            .ToList();

        return CommitDll(import.Package.Id, reports, force, persist: () =>
        {
            // Replace any existing entry with the same id (different version → upgrade).
            for (var i = _config.ExternalPackages.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_config.ExternalPackages[i].Id, import.Package.Id, StringComparison.OrdinalIgnoreCase))
                    _config.ExternalPackages.RemoveAt(i);
            }
            _config.ExternalPackages.Add(import.Package);
        });
    }

    /// <summary>
    /// Remove a loose DLL from the persisted list. The assembly stays loaded
    /// in the current session because the default AppDomain doesn't support
    /// unload; the change takes effect on the next launch.
    /// </summary>
    public void RemoveDll(string dllPath)
    {
        for (var i = _config.ExternalDlls.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_config.ExternalDlls[i], dllPath, StringComparison.OrdinalIgnoreCase))
                _config.ExternalDlls.RemoveAt(i);
        }
        _config.Save();
    }

    /// <summary>
    /// Remove a NuGet package from the persisted list and delete its folder
    /// in the package store. The assembly stays loaded in the current
    /// session (no unload in the default AppDomain) but won't be loaded on
    /// next launch.
    /// </summary>
    public void RemovePackage(ExternalPackageRef pkg)
    {
        for (var i = _config.ExternalPackages.Count - 1; i >= 0; i--)
        {
            var p = _config.ExternalPackages[i];
            if (string.Equals(p.Id, pkg.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Version, pkg.Version, StringComparison.OrdinalIgnoreCase))
                _config.ExternalPackages.RemoveAt(i);
        }
        var folder = Path.Combine(_packageStoreRoot, pkg.Id, pkg.Version);
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
        catch { /* best effort; user can clean up by hand */ }
        _config.Save();
    }

    private ExternalAddOutcome CommitDll(
        string sourceLabel,
        IReadOnlyList<AssemblyValidationReport> reports,
        bool force,
        Action persist)
    {
        var hasErrors = reports.Any(r => r.HasErrors);
        if (hasErrors && !force)
            return new ExternalAddOutcome(false, Array.Empty<string>(), reports, Array.Empty<string>());

        var loaded = new List<string>();
        var runtimeErrors = new List<string>();
        foreach (var r in reports)
        {
            if (!TryLoad(r.DllPath, out var err))
            {
                runtimeErrors.Add(err!);
                continue;
            }
            loaded.Add(r.DllPath);
        }

        if (loaded.Count == 0 && runtimeErrors.Count > 0)
            return new ExternalAddOutcome(false, loaded, reports, runtimeErrors);

        persist();
        _config.Save();
        return new ExternalAddOutcome(true, loaded, reports, runtimeErrors);
    }

    private bool TryLoad(string dllPath, out string? error)
    {
        try
        {
            var asm = Assembly.LoadFrom(dllPath);
            // Force the type system to bind so a ReflectionTypeLoadException
            // surfaces here instead of mid-script. Partial-load is acceptable
            // (model packages often ship optional types); we just record any
            // loader exceptions for the terminal.
            try { _ = asm.GetTypes(); }
            catch (ReflectionTypeLoadException rtle)
            {
                var first = rtle.LoaderExceptions.FirstOrDefault()?.Message;
                error = first is null
                    ? $"Loaded {Path.GetFileName(dllPath)} with type-load warnings."
                    : $"Loaded {Path.GetFileName(dllPath)}, but some types failed: {first}";
                _loadedDlls.Add(dllPath);
                return true;
            }
            _loadedDlls.Add(dllPath);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to load {Path.GetFileName(dllPath)}: {ex.Message}";
            return false;
        }
    }

    private static ExternalAddOutcome Failure(string message)
        => new(false, Array.Empty<string>(), Array.Empty<AssemblyValidationReport>(), new[] { message });
}
