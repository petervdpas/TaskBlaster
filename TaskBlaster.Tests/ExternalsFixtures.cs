using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace TaskBlaster.Tests;

/// <summary>
/// Test fixtures for the Externals layer. Builds throwaway <c>.dll</c>
/// files (via <see cref="PersistedAssemblyBuilder"/>) and synthesises
/// <c>.nupkg</c> zips around them so individual tests can mint exactly
/// the inputs they need without committing binary fixtures to git.
/// </summary>
internal static class ExternalsFixtures
{
    /// <summary>
    /// Emit a minimal assembly to <paramref name="dllPath"/> with the given
    /// identity. The assembly contains one empty type so it's not a degenerate
    /// zero-type DLL. Optionally references another simple-name+version pair
    /// (the referenced assembly does not need to exist on disk).
    /// </summary>
    public static void BuildDll(
        string dllPath,
        string assemblyName,
        Version version,
        (string Name, Version Version)? reference = null)
    {
        var name = new AssemblyName(assemblyName) { Version = version };
        var builder = new PersistedAssemblyBuilder(name, typeof(object).Assembly);
        var module  = builder.DefineDynamicModule(assemblyName);

        if (reference is { } r)
        {
            // Force a metadata reference to (Name, Version) by importing a
            // type from a stub builder with that identity. We can't easily
            // get the runtime to load a fake assembly, but we *can* declare
            // a base type via a TypeBuilder reference; the simplest cheat is
            // to attach a custom attribute whose constructor lives in the
            // referenced assembly. Even simpler: add a dummy AssemblyRef by
            // calling GetTypeForwardedTo... none of this is ergonomic.
            //
            // What actually works reliably: declare a local type whose base
            // class is loaded from a reference probe that we hand-craft via
            // CreateMember. Unfortunately PersistedAssemblyBuilder doesn't
            // expose a way to add a bare AssemblyRef without a real type.
            //
            // The validator only inspects GetReferencedAssemblies(), so we
            // instead synthesise the reference by deriving from a real type
            // in a real assembly — see BuildDllReferencingFile for that
            // path. The (name, version) overload only supports referencing
            // a stub assembly we've also written; the test wires it up by
            // calling BuildDllReferencingFile with a path produced by an
            // earlier BuildDll call.
            throw new NotSupportedException(
                "Use BuildDllReferencingFile to add a metadata reference to a real on-disk DLL.");
        }

        var t = module.DefineType($"{assemblyName}.Marker", TypeAttributes.Public | TypeAttributes.Class);
        t.CreateType();
        builder.Save(dllPath);
    }

    /// <summary>
    /// Like <see cref="BuildDll"/> but also adds a real metadata reference to
    /// the assembly at <paramref name="referencedDllPath"/> by deriving a
    /// type from one inside it. Lets the validator's
    /// <c>GetReferencedAssemblies()</c> walk see a non-trivial dependency.
    /// </summary>
    public static void BuildDllReferencingFile(
        string dllPath,
        string assemblyName,
        Version version,
        string referencedDllPath)
    {
        var name = new AssemblyName(assemblyName) { Version = version };
        var builder = new PersistedAssemblyBuilder(name, typeof(object).Assembly);
        var module  = builder.DefineDynamicModule(assemblyName);

        // Load the referenced assembly into the current AppDomain just long
        // enough to grab a Type from it; the metadata builder records the
        // assembly identity from that type, which becomes an AssemblyRef in
        // our emitted DLL.
        var refAsm = Assembly.LoadFrom(referencedDllPath);
        Type? baseType = null;
        foreach (var candidate in refAsm.GetExportedTypes())
        {
            if (candidate.IsClass && !candidate.IsSealed && !candidate.IsAbstract)
            {
                baseType = candidate;
                break;
            }
        }
        baseType ??= typeof(object);

        var t = module.DefineType($"{assemblyName}.Derived", TypeAttributes.Public | TypeAttributes.Class, baseType);
        t.CreateType();
        builder.Save(dllPath);
    }

    /// <summary>
    /// Build a synthetic <c>.nupkg</c> file (a zip) at <paramref name="nupkgPath"/>
    /// with the given id/version and a per-TFM dictionary of file paths to
    /// stage under <c>lib/&lt;tfm&gt;/</c>. The <see cref="NupkgImporter"/>
    /// only cares about the lib/ layout and the nuspec; everything else is
    /// optional.
    /// </summary>
    public static void BuildNupkg(
        string nupkgPath,
        string id,
        string version,
        Dictionary<string, string[]> filesByTfm)
    {
        var dir = Path.GetDirectoryName(nupkgPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(nupkgPath)) File.Delete(nupkgPath);

        using var fs = File.Create(nupkgPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        // Nuspec at root.
        var nuspec = $"""
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
              <metadata>
                <id>{id}</id>
                <version>{version}</version>
                <authors>tests</authors>
                <description>fixture</description>
              </metadata>
            </package>
            """;
        var nuspecEntry = zip.CreateEntry($"{id}.nuspec");
        using (var w = new StreamWriter(nuspecEntry.Open(), new UTF8Encoding(false)))
            w.Write(nuspec);

        // Lib files, one entry per (tfm, source path).
        foreach (var (tfm, sources) in filesByTfm)
        {
            foreach (var sourcePath in sources)
            {
                var entryName = $"lib/{tfm}/{Path.GetFileName(sourcePath)}";
                var entry = zip.CreateEntry(entryName);
                using var es = entry.Open();
                using var ss = File.OpenRead(sourcePath);
                ss.CopyTo(es);
            }
        }
    }

    /// <summary>
    /// Allocate a temp folder for a single test. Caller deletes it via
    /// <c>Directory.Delete(recursive: true)</c> in <c>Dispose</c>.
    /// </summary>
    public static string FreshTempFolder(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tb-{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
