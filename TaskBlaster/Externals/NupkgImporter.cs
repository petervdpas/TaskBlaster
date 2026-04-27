using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace TaskBlaster.Externals;

/// <summary>
/// Result of unpacking a <c>.nupkg</c> into TaskBlaster's package store.
/// </summary>
/// <param name="Package">Parsed identity from the nuspec.</param>
/// <param name="InstallFolder">Folder under <c>~/.taskblaster/packages/&lt;Id&gt;/&lt;Version&gt;/</c>.</param>
/// <param name="ChosenTfm">Which TFM subfolder we picked from the package's <c>lib/</c>.</param>
/// <param name="Dlls">Absolute paths to every <c>.dll</c> we extracted from the chosen TFM folder.</param>
public sealed record NupkgImportResult(
    ExternalPackageRef Package,
    string InstallFolder,
    string ChosenTfm,
    IReadOnlyList<string> Dlls);

/// <summary>
/// Unpacks a NuGet <c>.nupkg</c> (a zip) into TaskBlaster's package store.
/// We only keep the <c>lib/&lt;tfm&gt;/*.dll</c> files for the most-preferred
/// compatible TFM; everything else (build/, content/, _rels/, …) is ignored
/// because TaskBlaster only ever loads the assemblies for Roslyn.
/// </summary>
public static class NupkgImporter
{
    /// <summary>
    /// TFMs we'll happily load, most-preferred first. <c>net10.0</c> is the
    /// runtime we're on; the .NET-Standard tail covers older PCL-style
    /// canonical-model packages that haven't moved to a modern TFM yet.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedTfms = new[]
    {
        "net10.0", "net9.0", "net8.0", "net7.0", "net6.0",
        "netstandard2.1", "netstandard2.0",
    };

    /// <summary>
    /// Read the nuspec inside <paramref name="nupkgPath"/> and return the
    /// package identity, without extracting anything. Lets the UI confirm
    /// what's about to be added before commit.
    /// </summary>
    public static ExternalPackageRef ReadIdentity(string nupkgPath)
    {
        using var zip = ZipFile.OpenRead(nupkgPath);
        return ReadIdentityFromZip(zip);
    }

    /// <summary>
    /// Extract the package's <c>lib/&lt;best-tfm&gt;/*.dll</c> files into
    /// <paramref name="storeRoot"/><c>/&lt;Id&gt;/&lt;Version&gt;/</c>. If the
    /// destination folder already exists it's wiped first so re-imports give
    /// a clean tree (no stale DLLs from a previous version sneaking onto the
    /// load path).
    /// </summary>
    /// <exception cref="InvalidDataException">No nuspec at the root.</exception>
    /// <exception cref="NotSupportedException">No TFM in <see cref="SupportedTfms"/> present in the package's <c>lib/</c>.</exception>
    public static NupkgImportResult Import(string nupkgPath, string storeRoot)
    {
        using var zip = ZipFile.OpenRead(nupkgPath);
        var identity = ReadIdentityFromZip(zip);

        var libFolders = zip.Entries
            .Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            .Select(e => SecondSegment(e.FullName))
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var chosen = SupportedTfms.FirstOrDefault(t =>
            libFolders.Any(f => string.Equals(f, t, StringComparison.OrdinalIgnoreCase)));
        if (chosen is null)
        {
            var present = libFolders.Count == 0 ? "(none)" : string.Join(", ", libFolders);
            throw new NotSupportedException(
                $"No compatible TFM in package. Found: {present}. Need one of: {string.Join(", ", SupportedTfms)}.");
        }

        var installFolder = Path.Combine(storeRoot, identity.Id, identity.Version);
        if (Directory.Exists(installFolder))
            Directory.Delete(installFolder, recursive: true);
        Directory.CreateDirectory(installFolder);

        var prefix = $"lib/{chosen}/";
        var dlls = new List<string>();
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
            if (!entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;

            var dest = Path.Combine(installFolder, entry.Name);
            entry.ExtractToFile(dest, overwrite: true);
            dlls.Add(dest);
        }

        return new NupkgImportResult(identity, installFolder, chosen, dlls);
    }

    private static ExternalPackageRef ReadIdentityFromZip(ZipArchive zip)
    {
        var nuspec = zip.Entries.FirstOrDefault(e =>
            !e.FullName.Contains('/') &&
            e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("No .nuspec at the root of the package.");

        using var stream = nuspec.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var meta = doc.Root?.Element(ns + "metadata")
            ?? throw new InvalidDataException("nuspec has no <metadata> element.");

        var id      = (string?)meta.Element(ns + "id")      ?? throw new InvalidDataException("nuspec missing <id>.");
        var version = (string?)meta.Element(ns + "version") ?? throw new InvalidDataException("nuspec missing <version>.");
        return new ExternalPackageRef(id.Trim(), version.Trim());
    }

    private static string? SecondSegment(string fullName)
    {
        // "lib/net10.0/Foo.dll" -> "net10.0"
        var parts = fullName.Split('/');
        return parts.Length >= 3 ? parts[1] : null;
    }
}
