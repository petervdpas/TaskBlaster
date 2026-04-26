using System;
using System.Collections.Generic;
using System.Dynamic;

namespace TaskBlaster.Connections;

/// <summary>
/// Resolved view of one named connection — every field already turned
/// into a literal string value. Plaintext fields come from
/// <c>connections.json</c> directly; vault-ref fields are dereferenced
/// against the vault at call time. Vault unlock happens once on the
/// first vault-ref field if the vault is locked.
/// <para>
/// Inherits <see cref="DynamicObject"/> so scripts that hold the snapshot
/// as <c>dynamic</c> (the default when <see cref="ScriptSecrets.GetConnection"/>
/// returns it) can access fields with member-access syntax:
/// <code>
/// var conn = Secrets.GetConnection("github");
/// var url   = conn.baseUrl;   // dynamic dispatch → Fields["baseUrl"]
/// var token = conn.token;     // case-insensitive fallback also matches "Token"
/// </code>
/// Typed access (<see cref="this[string]"/>, <see cref="Fields"/>,
/// <see cref="GetOrDefault"/>) keeps working too.
/// </para>
/// </summary>
public sealed class ConnectionSnapshot : DynamicObject
{
    /// <summary>Connection name (the resolver "category" per the library convention).</summary>
    public string Name { get; }

    /// <summary>Resolved field values, keyed by field name (case-sensitive, matches the on-disk JSON).</summary>
    public IReadOnlyDictionary<string, string> Fields { get; }

    public ConnectionSnapshot(string name, IReadOnlyDictionary<string, string> fields)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Fields = fields ?? throw new ArgumentNullException(nameof(fields));
    }

    /// <summary>Get a field value by name. Throws <see cref="KeyNotFoundException"/> if the field isn't defined.</summary>
    public string this[string field] => Fields[field];

    /// <summary>Get a field value, or the supplied default when the field isn't defined.</summary>
    public string GetOrDefault(string field, string fallback = "")
        => Fields.TryGetValue(field, out var v) ? v : fallback;

    /// <summary>True when the connection defines a field with the given name.</summary>
    public bool Has(string field) => Fields.ContainsKey(field);

    /// <summary>
    /// DLR hook: <c>conn.fieldName</c> on a <c>dynamic</c> reference
    /// routes here. Tries an exact-case lookup first (matching the
    /// stored key), then a case-insensitive one so scripts can write
    /// <c>conn.token</c> against a <c>"Token"</c> key without surprise.
    /// Returns <c>false</c> on a miss so the DLR raises its standard
    /// "no member" error.
    /// </summary>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (Fields.TryGetValue(binder.Name, out var v))
        {
            result = v;
            return true;
        }
        foreach (var (key, val) in Fields)
        {
            if (string.Equals(key, binder.Name, StringComparison.OrdinalIgnoreCase))
            {
                result = val;
                return true;
            }
        }
        result = null;
        return false;
    }

    /// <summary>Lets debuggers and tooling enumerate the dynamic field names.</summary>
    public override IEnumerable<string> GetDynamicMemberNames() => Fields.Keys;
}
