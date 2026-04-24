using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskBlaster.Secrets;

/// <summary>
/// Encrypted list of category names the user has configured. Stored inside
/// the vault as a single reserved record under <see cref="ReservedId"/>, so
/// category names are protected the same way secret values are.
/// </summary>
public sealed record CategoryCatalog(
    int SchemaVersion,
    IReadOnlyList<string> Categories,
    DateTime UpdatedUtc)
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Reserved SecretBlast name for the catalog record. Fixed 32-char hex so
    /// its on-disk filename looks identical to every other <c>*.secret</c> and
    /// the vault layout stays uniform.
    /// </summary>
    public const string ReservedId = "00000000000000000000000000000001";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static CategoryCatalog Empty { get; } = new(CurrentSchemaVersion, Array.Empty<string>(), DateTime.UtcNow);

    public static CategoryCatalog Create(IEnumerable<string> categories, DateTime? nowUtc = null)
    {
        var normalized = Normalize(categories);
        return new CategoryCatalog(CurrentSchemaVersion, normalized, nowUtc ?? DateTime.UtcNow);
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static CategoryCatalog FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidCategoryCatalogException("Catalog JSON is empty.");

        CategoryCatalog? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<CategoryCatalog>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidCategoryCatalogException("Catalog JSON is malformed.", ex);
        }
        if (parsed is null)
            throw new InvalidCategoryCatalogException("Catalog JSON deserialized to null.");

        if (parsed.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidCategoryCatalogException(
                $"Unsupported catalog schema version {parsed.SchemaVersion}; expected {CurrentSchemaVersion}.");

        // Defensive copy + normalise: trim, drop blanks, dedupe, preserve first-seen order.
        return parsed with { Categories = Normalize(parsed.Categories ?? Array.Empty<string>()) };
    }

    /// <summary>
    /// Trim each name, drop empties, dedupe case-insensitively (preserving the
    /// first-seen casing), and sort alphabetically. The sort keeps pickers
    /// predictable and eliminates insertion-order churn when callers pass
    /// merged lists.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> names)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();
        foreach (var raw in names)
        {
            var trimmed = (raw ?? string.Empty).Trim();
            if (trimmed.Length == 0) continue;
            if (!seen.Add(trimmed)) continue;
            kept.Add(trimmed);
        }
        kept.Sort(StringComparer.OrdinalIgnoreCase);
        return kept;
    }
}

public sealed class InvalidCategoryCatalogException : Exception
{
    public InvalidCategoryCatalogException(string message) : base(message) { }
    public InvalidCategoryCatalogException(string message, Exception inner) : base(message, inner) { }
}
