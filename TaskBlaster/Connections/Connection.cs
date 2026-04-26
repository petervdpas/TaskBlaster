using System.Collections.Generic;

namespace TaskBlaster.Connections;

/// <summary>
/// A vault-backed reference to a secret value.
/// Used inside <see cref="ConnectionField"/> when the value should be
/// read from the encrypted vault rather than stored as plaintext.
/// </summary>
public sealed record ConnectionVaultRef(string Category, string Key);

/// <summary>
/// One field of a <see cref="Connection"/>. Either a plaintext literal
/// (URL, server name, timeout) lives in <see cref="Value"/>, or a vault
/// pointer lives in <see cref="FromVault"/>. Exactly one is non-null on
/// a well-formed field; malformed fields are dropped at load.
/// </summary>
public sealed record ConnectionField(string? Value, ConnectionVaultRef? FromVault)
{
    /// <summary>True when the field stores its value as plaintext.</summary>
    public bool IsPlaintext => Value is not null;

    /// <summary>True when the field is a pointer into the vault.</summary>
    public bool IsFromVault => FromVault is not null;

    /// <summary>Build a plaintext field.</summary>
    public static ConnectionField Plaintext(string value) => new(value, null);

    /// <summary>Build a vault-backed field.</summary>
    public static ConnectionField OfVault(string category, string key)
        => new(null, new ConnectionVaultRef(category, key));
}

/// <summary>
/// A named bag of fields. Library convention (per
/// <c>networkblast_plan.md</c>): the connection name is the resolver
/// category, and per-field keys are the well-known names the library
/// asks for (e.g. <c>baseUrl</c>, <c>token</c>).
/// </summary>
public sealed record Connection(string Name, IReadOnlyDictionary<string, ConnectionField> Fields);
