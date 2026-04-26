using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Connections;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

/// <summary>
/// Behavioural tests for <see cref="ConnectionsResolver"/> — the four
/// branches a library-side <c>resolver(category, key)</c> call can take
/// against a connections file: plaintext, fromVault, missing-field,
/// missing-connection.
/// </summary>
public sealed class ConnectionsResolverTests
{
    [Fact]
    public async Task Plaintext_ReturnsLiteral_WithoutHittingVault()
    {
        var store = new InMemoryConnectionStore();
        store.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
        }));

        var vaultCalls = 0;
        Func<string, string, CancellationToken, Task<string>> vault = (_, _, _) =>
        {
            vaultCalls++;
            return Task.FromResult("vault-value");
        };

        var resolver = new ConnectionsResolver(store, vault);
        var got = await resolver.ResolveAsync("github", "baseUrl");

        Assert.Equal("https://api.github.com", got);
        Assert.Equal(0, vaultCalls);
    }

    [Fact]
    public async Task FromVault_DispatchesToUnderlyingResolver_WithPointedToCategoryKey()
    {
        var store = new InMemoryConnectionStore();
        store.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["token"] = ConnectionField.OfVault("github-secrets", "pat"),
        }));

        (string Category, string Key)? seen = null;
        Func<string, string, CancellationToken, Task<string>> vault = (cat, key, _) =>
        {
            seen = (cat, key);
            return Task.FromResult("ghp_xyz");
        };

        var resolver = new ConnectionsResolver(store, vault);
        var got = await resolver.ResolveAsync("github", "token");

        Assert.Equal("ghp_xyz", got);
        Assert.Equal(("github-secrets", "pat"), seen);
    }

    [Fact]
    public async Task MissingField_FallsThroughToVaultResolver_WithOriginalCategoryKey()
    {
        // Connection exists but doesn't define the asked-for field — fall
        // through so the library's well-known key still resolves against
        // the vault if a (category, key) entry happens to exist there.
        var store = new InMemoryConnectionStore();
        store.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
        }));

        (string Category, string Key)? seen = null;
        Func<string, string, CancellationToken, Task<string>> vault = (cat, key, _) =>
        {
            seen = (cat, key);
            return Task.FromResult("from-vault");
        };

        var resolver = new ConnectionsResolver(store, vault);
        var got = await resolver.ResolveAsync("github", "token");

        Assert.Equal("from-vault", got);
        Assert.Equal(("github", "token"), seen);
    }

    [Fact]
    public async Task MissingConnection_FallsThroughToVaultResolver_Unchanged()
    {
        // No connections file at all (or no entry for this name) — behave
        // exactly as the raw vault resolver. Existing all-vault setups
        // must keep working untouched.
        var store = new InMemoryConnectionStore();

        Func<string, string, CancellationToken, Task<string>> vault = (cat, key, _) =>
            Task.FromResult($"{cat}/{key}");

        var resolver = new ConnectionsResolver(store, vault);
        var got = await resolver.ResolveAsync("azure", "prod-sql");

        Assert.Equal("azure/prod-sql", got);
    }

    [Fact]
    public async Task Resolver_DelegateShape_MatchesFuncSignature()
    {
        // The ".Resolver" property is what a library actually receives.
        // This test asserts the wrapper exposes it as a Func<string, string,
        // CancellationToken, Task<string>>, so existing libraries that take
        // that shape (NetworkBlast, AzureBlast) get it without conversion.
        var store = new InMemoryConnectionStore();
        Func<string, string, CancellationToken, Task<string>> vault = (_, _, _) => Task.FromResult("ok");
        var resolver = new ConnectionsResolver(store, vault);

        Func<string, string, CancellationToken, Task<string>> handed = resolver.Resolver;
        var got = await handed("any", "thing", CancellationToken.None);

        Assert.Equal("ok", got);
    }

    /// <summary>In-memory <see cref="IConnectionStore"/>, no disk.</summary>
    private sealed class InMemoryConnectionStore : IConnectionStore
    {
        private readonly Dictionary<string, Connection> _byName = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<Connection> List() => _byName.Values.ToList();
        public Connection? Get(string name) =>
            string.IsNullOrEmpty(name) ? null : _byName.TryGetValue(name, out var c) ? c : null;
        public void Save(Connection connection) => _byName[connection.Name] = connection;
        public void Remove(string name) => _byName.Remove(name);
        public void Reload() { /* no-op */ }
    }
}
