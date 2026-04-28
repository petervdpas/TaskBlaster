using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AgentBlast.Prompts;
using TaskBlaster.Externals;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Engine;

/// <summary>
/// Walks <see cref="AppDomain.CurrentDomain"/> and produces a structured
/// snapshot of every loadable assembly (filtering out ghosts whose
/// backing files have disappeared, matching the same hardening applied
/// to <see cref="ScriptBlaster"/>'s loadable-assemblies enumeration).
/// The catalog produces AgentBlast's <see cref="LoadedReference"/> shape
/// directly, so its output can be handed straight to
/// <see cref="PromptBuilder"/> without conversion.
///
/// Reads the Blast-family <c>[AssemblyMetadata("Blast.PrimaryFacade", "...")]</c>
/// convention so the AI assistant can identify a package's canonical
/// front-door types (<c>NetClient</c>, <c>MssqlDatabase</c>, etc.) without
/// scanning every public type.
/// </summary>
public sealed class LoadedReferenceCatalog
{
    /// <summary>The MSBuild attribute key Blast nugets stamp with their front-door type names.</summary>
    public const string PrimaryFacadeKey = "Blast.PrimaryFacade";

    private readonly IConfigStore _config;
    private readonly ExternalReferenceManager _externals;
    private readonly string _runtimeDir;
    private readonly string _appBaseDir;

    public LoadedReferenceCatalog(IConfigStore config, ExternalReferenceManager externals)
    {
        _config = config;
        _externals = externals;
        _runtimeDir = NormalizeFolder(RuntimeEnvironment.GetRuntimeDirectory());
        _appBaseDir = NormalizeFolder(AppContext.BaseDirectory);
    }

    /// <summary>
    /// Snapshot every currently-loaded assembly. Cheap-ish (a few ms on a
    /// busy AppDomain). Ghost assemblies (file deleted out from under us)
    /// are filtered out, same as <see cref="ScriptBlaster"/>'s reference
    /// list, so callers can trust every <see cref="LoadedReference.Location"/>
    /// points at a real file.
    /// </summary>
    public IReadOnlyList<LoadedReference> Snapshot()
    {
        var results = new List<LoadedReference>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;

            string location;
            try { location = asm.Location; }
            catch { continue; }
            if (string.IsNullOrEmpty(location) || !File.Exists(location)) continue;

            var name = asm.GetName();
            var facades = ReadPrimaryFacades(asm);
            var origin = Classify(location, facades.Count > 0);

            results.Add(new LoadedReference(
                Name:           name.Name ?? Path.GetFileNameWithoutExtension(location),
                Version:        name.Version?.ToString() ?? "?",
                Location:       location,
                Origin:         origin,
                PrimaryFacades: facades,
                Namespaces:     EnumerateNamespaces(asm)));
        }
        return results;
    }

    /// <summary>
    /// Filter helper: most callers only care about a subset (e.g. just
    /// Blast + External when offering "what types can I use"). Cheap LINQ
    /// wrapper so call sites read clearly without re-typing the predicate.
    /// </summary>
    public IReadOnlyList<LoadedReference> SnapshotByOrigin(params LoadedReferenceOrigin[] origins)
    {
        var set = origins is { Length: > 0 } ? new HashSet<LoadedReferenceOrigin>(origins) : null;
        return Snapshot().Where(r => set is null || set.Contains(r.Origin)).ToList();
    }

    private LoadedReferenceOrigin Classify(string location, bool hasPrimaryFacade)
    {
        // Blast wins on attribute, regardless of file path. A Blast nuget
        // restored to ~/.nuget/packages still reads as Blast, not as
        // Application or Framework — the attribute is the source of truth.
        if (hasPrimaryFacade) return LoadedReferenceOrigin.Blast;

        var normalized = NormalizeFolder(Path.GetDirectoryName(location) ?? location);

        if (IsUnderPackageStore(normalized) || IsExternalDll(location))
            return LoadedReferenceOrigin.External;

        if (StartsWithFolder(normalized, _runtimeDir))
            return LoadedReferenceOrigin.Framework;

        if (StartsWithFolder(normalized, _appBaseDir))
            return LoadedReferenceOrigin.Application;

        return LoadedReferenceOrigin.Other;
    }

    private bool IsUnderPackageStore(string folder)
    {
        var root = NormalizeFolder(_externals.PackageStoreRoot);
        return StartsWithFolder(folder, root);
    }

    private bool IsExternalDll(string location)
        => _config.ExternalDlls.Any(d => string.Equals(d, location, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Static so callers can parse a facade list out of any assembly without
    /// instantiating the whole catalog. Comma-separated, fully-qualified
    /// type names (the attribute value we stamp on each Blast nuget).
    /// </summary>
    public static IReadOnlyList<string> ReadPrimaryFacades(Assembly asm)
    {
        var attr = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, PrimaryFacadeKey, StringComparison.Ordinal));
        if (attr?.Value is not { Length: > 0 } value) return Array.Empty<string>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> EnumerateNamespaces(Assembly asm)
    {
        try
        {
            return asm.GetExportedTypes()
                .Select(t => t.Namespace ?? string.Empty)
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
        }
        catch
        {
            // Partially-loaded assemblies refuse GetExportedTypes(); don't
            // let one bad apple kill the whole snapshot.
            return Array.Empty<string>();
        }
    }

    private static string NormalizeFolder(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed + Path.DirectorySeparatorChar;
    }

    private static bool StartsWithFolder(string candidate, string folder)
        => candidate.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
}
