using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TaskBlaster.Engine;

/// <summary>
/// One named parameter on a documented member.
/// </summary>
public sealed record XmlDocParam(string Name, string Description);

/// <summary>
/// Parsed contents of one <c>&lt;member&gt;</c> element from an xmldoc
/// file. <see cref="MemberId"/> is the standard ECMA-335 member ID
/// string (e.g. <c>"T:Acme.Domain.Customer"</c>,
/// <c>"M:Acme.Domain.Order.#ctor(System.String,...)"</c>).
/// </summary>
public sealed record XmlDocEntry(
    string MemberId,
    string? Summary,
    string? Remarks,
    string? Returns,
    IReadOnlyList<XmlDocParam> Parameters);

/// <summary>
/// All xmldoc entries for one assembly's <c>.xml</c> file. Built once
/// per assembly via <see cref="XmlDocReader.TryRead"/>; cheap to
/// re-query via <see cref="Find"/>.
/// </summary>
public sealed class XmlDocSet
{
    private readonly Dictionary<string, XmlDocEntry> _byId;

    public XmlDocSet(string assemblyName, IReadOnlyList<XmlDocEntry> entries)
    {
        AssemblyName = assemblyName;
        Entries = entries;
        _byId = entries.ToDictionary(e => e.MemberId, StringComparer.Ordinal);
    }

    /// <summary>The assembly name as declared in the xmldoc <c>&lt;assembly&gt;&lt;name&gt;</c> element.</summary>
    public string AssemblyName { get; }

    /// <summary>Every parsed entry, in source order.</summary>
    public IReadOnlyList<XmlDocEntry> Entries { get; }

    /// <summary>Look up by ECMA-335 member ID; returns null if not present.</summary>
    public XmlDocEntry? Find(string memberId)
        => _byId.TryGetValue(memberId, out var e) ? e : null;
}

/// <summary>
/// Reads C# xmldoc XML files and turns them into queryable
/// <see cref="XmlDocSet"/>s. NuGet packages ship the <c>.xml</c> file
/// alongside the <c>.dll</c>, so given a loaded assembly's location
/// we can almost always find it.
///
/// Designed to be the documentation layer the eventual AI assistant
/// feeds an LLM (so type / method descriptions ride along with the
/// signatures); also useful for editor tooltips today.
/// </summary>
public static class XmlDocReader
{
    /// <summary>
    /// Look for a <c>.xml</c> file alongside <paramref name="dllPath"/> and
    /// parse it. Returns null if no doc file is present (loose DLLs and
    /// many older nugets don't ship one).
    /// </summary>
    public static XmlDocSet? TryRead(string dllPath)
    {
        var xmlPath = Path.ChangeExtension(dllPath, ".xml");
        if (!File.Exists(xmlPath)) return null;
        try
        {
            return Parse(File.ReadAllText(xmlPath), Path.GetFileNameWithoutExtension(dllPath));
        }
        catch
        {
            // Malformed xmldoc shouldn't kill the caller — they can still
            // get type info from reflection. Treat as "no docs".
            return null;
        }
    }

    /// <summary>
    /// Parse a raw xmldoc string into a set. Public so callers with the
    /// XML content already in hand (e.g. from a stream, a test fixture)
    /// can skip the file-system probe.
    /// </summary>
    public static XmlDocSet Parse(string xmlContent, string fallbackAssemblyName)
    {
        var doc = XDocument.Parse(xmlContent);
        var root = doc.Root ?? throw new InvalidDataException("xmldoc has no root element.");

        var assemblyName = root.Element("assembly")?.Element("name")?.Value?.Trim()
            ?? fallbackAssemblyName;

        var entries = new List<XmlDocEntry>();
        var members = root.Element("members");
        if (members is not null)
        {
            foreach (var m in members.Elements("member"))
            {
                var id = m.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(id)) continue;

                entries.Add(new XmlDocEntry(
                    MemberId:   id,
                    Summary:    NormalizeText(m.Element("summary")),
                    Remarks:    NormalizeText(m.Element("remarks")),
                    Returns:    NormalizeText(m.Element("returns")),
                    Parameters: m.Elements("param")
                                 .Select(p => new XmlDocParam(
                                     Name:        p.Attribute("name")?.Value ?? "",
                                     Description: NormalizeText(p) ?? ""))
                                 .Where(p => p.Name.Length > 0)
                                 .ToList()));
            }
        }

        return new XmlDocSet(assemblyName, entries);
    }

    /// <summary>
    /// Collapse the kind of whitespace xmldoc files always have (leading
    /// blanks per line because the C# compiler emits one tag per source
    /// line) into a single trimmed paragraph. Preserves inter-line spacing
    /// as single spaces so multi-line summaries don't read glued together.
    /// </summary>
    private static string? NormalizeText(XElement? element)
    {
        if (element is null) return null;
        // Use Value (concatenates all descendant text) so embedded tags
        // like <c>, <see cref="..."/> still contribute readable content.
        var raw = element.Value;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var lines = raw.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        var joined = string.Join(' ', lines).Trim();
        return joined.Length > 0 ? joined : null;
    }
}
