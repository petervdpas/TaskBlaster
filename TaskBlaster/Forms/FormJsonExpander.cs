using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Forms;

/// <summary>
/// Preprocessor that turns a raw TaskBlaster form JSON — one that may
/// contain <c>optionsFrom</c> hints — into a plain GuiBlast-compatible
/// form JSON with materialised <c>options</c> arrays. GuiBlast itself
/// stays vault-agnostic; every form load runs through here first.
///
/// Today understood sources:
/// <list type="bullet">
///   <item><description><c>{ "source": "vault", "category": "Azure" }</c>
///     — replaced by <c>{ value, label }</c> pairs, one per key in that
///     vault category. Values are never materialised; only keys leave
///     the vault wrapper.</description></item>
/// </list>
///
/// Forms without any <c>optionsFrom</c> hint round-trip unchanged.
/// </summary>
public static class FormJsonExpander
{
    /// <summary>
    /// Walk <paramref name="json"/>, resolve any <c>optionsFrom</c>
    /// hints using <paramref name="vault"/>, and return the rewritten
    /// JSON. Leaves the input string alone (returns it as-is) when the
    /// document has no dynamic hints, so the common case is cheap.
    /// </summary>
    public static async Task<string> ExpandAsync(
        string json,
        IVaultService vault,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;

        var root = JsonNode.Parse(json);
        if (root is not JsonObject obj) return json;

        var fields = obj["fields"] as JsonArray;
        if (fields is null || fields.Count == 0) return json;

        var touched = false;
        foreach (var fieldNode in fields.OfType<JsonObject>())
        {
            var hint = fieldNode["optionsFrom"] as JsonObject;
            if (hint is null) continue;

            var source = (string?)hint["source"];
            if (string.IsNullOrWhiteSpace(source)) continue;

            // If the user already picked a subset of options in the designer,
            // keep those as-is — the hint just tags the field as vault-backed
            // for future editing. Only auto-fill when the list is empty.
            var existingOptions = fieldNode["options"] as JsonArray;
            if (existingOptions is null || existingOptions.Count == 0)
            {
                fieldNode["options"] = await ResolveAsync(source, hint, vault, ct).ConfigureAwait(false);
            }

            // Strip the hint — GuiBlast sees a plain `options: [...]` field
            // and nothing about the dynamic source it came from.
            fieldNode.Remove("optionsFrom");
            touched = true;
        }

        return touched ? root.ToJsonString() : json;
    }

    private static async Task<JsonArray> ResolveAsync(
        string source,
        JsonObject hint,
        IVaultService vault,
        CancellationToken ct)
    {
        return source.Trim().ToLowerInvariant() switch
        {
            "vault" => await VaultKeysAsync(hint, vault, ct).ConfigureAwait(false),
            _       => throw new NotSupportedException(
                $"Unknown optionsFrom source '{source}'. Expected one of: vault."),
        };
    }

    private static async Task<JsonArray> VaultKeysAsync(
        JsonObject hint,
        IVaultService vault,
        CancellationToken ct)
    {
        var category = ((string?)hint["category"] ?? string.Empty).Trim();
        if (category.Length == 0)
            throw new InvalidOperationException(
                "optionsFrom.source = 'vault' requires a non-empty 'category'.");

        var all = await vault.ListAsync(ct).ConfigureAwait(false);
        var arr = new JsonArray();
        foreach (var entry in all)
        {
            if (!string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase)) continue;
            arr.Add(new JsonObject
            {
                ["value"] = entry.Key,
                ["label"] = entry.Key,
            });
        }
        return arr;
    }
}
