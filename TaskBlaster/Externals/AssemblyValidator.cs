using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TaskBlaster.Externals;

/// <summary>Severity of an issue raised by <see cref="AssemblyValidator"/>.</summary>
public enum IssueLevel
{
    /// <summary>Adding the assembly is unsafe and should be blocked unless the user explicitly overrides.</summary>
    Error,
    /// <summary>Adding is allowed but something is likely to misbehave at runtime.</summary>
    Warning,
}

/// <summary>One finding from <see cref="AssemblyValidator"/>.</summary>
public sealed record AssemblyIssue(IssueLevel Level, string Message);

/// <summary>
/// Static-analysis report for a candidate DLL: identity, the issues we
/// found (TFM mismatches, missing references, version conflicts), plus a
/// quick <see cref="HasErrors"/> flag for the dialog to enable / disable
/// the OK button.
/// </summary>
public sealed record AssemblyValidationReport(
    string DllPath,
    string AssemblyName,
    string AssemblyVersion,
    IReadOnlyList<AssemblyIssue> Issues)
{
    public bool HasErrors   => Issues.Any(i => i.Level == IssueLevel.Error);
    public bool HasWarnings => Issues.Any(i => i.Level == IssueLevel.Warning);
}

/// <summary>
/// Inspects a candidate DLL using <see cref="MetadataLoadContext"/> so we
/// never pollute the runtime AppDomain just to find out an assembly is
/// broken. Reports TFM compatibility, unresolved references, and
/// identity-name version conflicts against everything already loaded
/// (AppDomain + previously-imported externals).
/// </summary>
public static class AssemblyValidator
{
    /// <summary>
    /// Statically inspect <paramref name="dllPath"/>. Pass every DLL from
    /// already-imported externals via <paramref name="loadedExternalDlls"/>
    /// so we can resolve same-package siblings and also flag conflicts
    /// against them. The current AppDomain is consulted automatically.
    /// </summary>
    public static AssemblyValidationReport Inspect(
        string dllPath,
        IEnumerable<string> loadedExternalDlls)
    {
        var issues = new List<AssemblyIssue>();

        // Build a resolver that can satisfy references from: the runtime
        // BCL (System.Runtime + co), every assembly currently loaded into
        // the live AppDomain, the candidate's own folder (for sibling DLLs
        // shipped in the same lib/<tfm>/ alongside it), and every DLL from
        // other externals the user has already imported.
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.EnumerateFiles(runtimeDir, "*.dll")) paths.Add(f);
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { if (!string.IsNullOrEmpty(asm.Location)) paths.Add(asm.Location); }
            catch { /* dynamic assembly */ }
        }
        var siblingFolder = Path.GetDirectoryName(dllPath);
        if (!string.IsNullOrEmpty(siblingFolder))
            foreach (var f in Directory.EnumerateFiles(siblingFolder, "*.dll")) paths.Add(f);
        foreach (var f in loadedExternalDlls) paths.Add(f);

        using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths));
        Assembly probe;
        try
        {
            probe = mlc.LoadFromAssemblyPath(dllPath);
        }
        catch (Exception ex)
        {
            return new AssemblyValidationReport(
                dllPath, Path.GetFileNameWithoutExtension(dllPath), "?",
                new[] { new AssemblyIssue(IssueLevel.Error, $"Cannot read metadata: {ex.Message}") });
        }

        var probeName = probe.GetName();
        var identityName    = probeName.Name    ?? Path.GetFileNameWithoutExtension(dllPath);
        var identityVersion = probeName.Version?.ToString() ?? "?";

        CheckTargetFramework(probe, issues);
        CheckSelfConflict(probeName, loadedExternalDlls, issues);
        CheckReferences(probe, mlc, paths, issues);

        return new AssemblyValidationReport(dllPath, identityName, identityVersion, issues);
    }

    private static void CheckTargetFramework(Assembly probe, List<AssemblyIssue> issues)
    {
        var tfm = probe.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;
        if (tfm is null) return; // older assemblies (esp. netstandard1.x) sometimes omit it

        // Heuristic: anything not in the .NETCoreApp / .NETStandard family is
        // very unlikely to load on net10.0. Old .NETFramework references that
        // happen to be type-only might still work, but flag it.
        if (tfm.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new AssemblyIssue(IssueLevel.Warning,
                $"Built for {tfm}; .NET Framework binaries usually fail to load on .NET 10."));
        }
    }

    private static void CheckSelfConflict(
        AssemblyName probeName,
        IEnumerable<string> loadedExternalDlls,
        List<AssemblyIssue> issues)
    {
        // Already loaded into the live AppDomain at a different version?
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var n = asm.GetName();
            if (!string.Equals(n.Name, probeName.Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (n.Version != probeName.Version)
            {
                issues.Add(new AssemblyIssue(IssueLevel.Error,
                    $"{probeName.Name} v{probeName.Version} conflicts with already-loaded v{n.Version} (TaskBlaster's own dependency)."));
            }
            return;
        }

        // Already imported via the External tab at a different version?
        foreach (var path in loadedExternalDlls)
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), probeName.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var existing = AssemblyName.GetAssemblyName(path);
                if (existing.Version != probeName.Version)
                {
                    issues.Add(new AssemblyIssue(IssueLevel.Error,
                        $"{probeName.Name} v{probeName.Version} conflicts with already-imported v{existing.Version} at {path}."));
                }
            }
            catch
            {
                // If we can't read the existing one we'll find out at load
                // time; not worth flagging here.
            }
        }
    }

    private static void CheckReferences(
        Assembly probe,
        MetadataLoadContext mlc,
        IReadOnlySet<string> resolverPaths,
        List<AssemblyIssue> issues)
    {
        foreach (var refName in probe.GetReferencedAssemblies())
        {
            // Try to resolve via metadata first (this is what the resolver
            // would actually do at runtime). If it loads, compare versions.
            Assembly? resolved = null;
            try { resolved = mlc.LoadFromAssemblyName(refName); }
            catch { /* unresolved — we'll report below */ }

            if (resolved is null)
            {
                issues.Add(new AssemblyIssue(IssueLevel.Warning,
                    $"References {refName.Name} v{refName.Version} but no copy is loadable; methods that touch it will throw at runtime."));
                continue;
            }

            var resolvedVersion = resolved.GetName().Version;
            if (resolvedVersion is null || refName.Version is null) continue;
            if (resolvedVersion < refName.Version)
            {
                issues.Add(new AssemblyIssue(IssueLevel.Warning,
                    $"References {refName.Name} v{refName.Version} but only v{resolvedVersion} is available; runtime may bind to a missing member."));
            }
        }
    }
}
