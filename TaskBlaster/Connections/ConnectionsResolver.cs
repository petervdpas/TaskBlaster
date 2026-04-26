using System;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Connections;

/// <summary>
/// Wraps a vault resolver with a connection-file overlay. For each
/// <c>(category, key)</c> lookup, checks <c>connections[category][key]</c>
/// first. Plaintext fields return their literal; vault-ref fields
/// dispatch to the underlying vault resolver against the pointed-to
/// <c>(category, key)</c>. Misses fall through to the vault directly so
/// all-vault setups (no connections file, or no entry for the looked-up
/// name) keep working unchanged.
/// </summary>
public sealed class ConnectionsResolver
{
    private readonly IConnectionStore _connections;
    private readonly Func<string, string, CancellationToken, Task<string>> _vaultResolver;

    public ConnectionsResolver(
        IConnectionStore connections,
        Func<string, string, CancellationToken, Task<string>> vaultResolver)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _vaultResolver = vaultResolver ?? throw new ArgumentNullException(nameof(vaultResolver));
    }

    /// <summary>
    /// The wrapped lambda, shaped like the resolver delegate libraries
    /// (NetworkBlast, AzureBlast) accept. Hand this directly to a
    /// library; it will look up plaintext fields without ever touching
    /// the vault and only unlock when an actual vault read is needed.
    /// </summary>
    public Func<string, string, CancellationToken, Task<string>> Resolver => ResolveAsync;

    /// <summary>Resolve a (category, key) lookup against connections + vault.</summary>
    public async Task<string> ResolveAsync(string category, string key, CancellationToken ct = default)
    {
        var conn = _connections.Get(category);
        if (conn is not null && conn.Fields.TryGetValue(key, out var field))
        {
            if (field.Value is not null)
                return field.Value;
            if (field.FromVault is not null)
                return await _vaultResolver(field.FromVault.Category, field.FromVault.Key, ct).ConfigureAwait(false);
        }
        return await _vaultResolver(category, key, ct).ConfigureAwait(false);
    }
}
