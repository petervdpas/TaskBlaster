using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Connections;

/// <summary>
/// Wraps a vault resolver with a connection-file overlay. A declared
/// connection is authoritative for its name: plaintext fields return
/// their literal, <c>fromVault</c> fields dispatch to the underlying
/// vault resolver, and any key the connection does not declare returns
/// the empty string. The vault unlock prompt is gated on the connection
/// itself: as soon as a connection that contains <em>any</em>
/// <c>fromVault</c> field is consulted, the resolver primes the vault by
/// resolving one of its vault refs, so the user is prompted up-front
/// even if the actual key being asked for is plaintext. Pure-plaintext
/// connections never touch the vault. Lookups against a name that isn't
/// in the file fall through to the vault directly, so all-vault setups
/// keep working unchanged.
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
        if (conn is null)
            return await _vaultResolver(category, key, ct).ConfigureAwait(false);

        var prime = conn.Fields.Values.FirstOrDefault(f => f.FromVault is not null)?.FromVault;
        if (prime is not null)
            _ = await _vaultResolver(prime.Category, prime.Key, ct).ConfigureAwait(false);

        if (!conn.Fields.TryGetValue(key, out var field))
            return string.Empty;

        if (field.Value is not null)
            return field.Value;

        if (field.FromVault is not null)
            return await _vaultResolver(field.FromVault.Category, field.FromVault.Key, ct).ConfigureAwait(false);

        return string.Empty;
    }
}
