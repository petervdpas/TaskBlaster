using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecretBlast;
using TaskBlaster.Connections;
using TaskBlaster.Engine;
using TaskBlaster.Interfaces;
using TaskBlaster.Secrets;

namespace TaskBlaster.Tests;

/// <summary>
/// Tests for the <see cref="ScriptSecrets"/> connections layer:
/// <c>Connections()</c> listing, <c>GetConnection()</c> resolution
/// across plaintext and vault-ref fields, and missing-connection error
/// handling. The vault is real (small Argon2 params) so the
/// vault-ref path actually decrypts.
/// </summary>
[Collection("ScriptBlaster")]
public sealed class ScriptSecretsConnectionsTests : IDisposable
{
    private readonly string _root;
    private readonly TestConfigStore _config;
    private readonly VaultService _vault;
    private readonly InMemoryConnectionStore _connections;
    private readonly ScriptSecrets _secrets;

    public ScriptSecretsConnectionsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tb-ssc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _config = new TestConfigStore(Path.Combine(_root, "vault"));
        _vault = new VaultService(_config, () => new VaultOptions
        {
            AutoLockIdle = Timeout.InfiniteTimeSpan,
            Kdf          = new Argon2Parameters(MemoryKiB: 1024, Iterations: 1, Parallelism: 1),
        });
        _connections = new InMemoryConnectionStore();
        _secrets = new ScriptSecrets(_vault, _ => Task.CompletedTask, _connections);
    }

    public void Dispose()
    {
        _vault.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task Connections_ReturnsRegisteredNames()
    {
        await _vault.InitializeAsync("pw");
        _connections.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
        }));
        _connections.Save(new Connection("prod-sql", new Dictionary<string, ConnectionField>
        {
            ["server"] = ConnectionField.Plaintext("tcp:..."),
        }));

        var names = _secrets.Connections();
        Assert.Contains("github", names);
        Assert.Contains("prod-sql", names);
    }

    [Fact]
    public async Task GetConnection_ResolvesPlaintextWithoutTouchingVault()
    {
        // Vault not even initialized — plaintext-only connections must work.
        _connections.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
            ["timeout"] = ConnectionField.Plaintext("30"),
        }));

        var conn = await _secrets.GetConnectionAsync("github");
        Assert.Equal("github", conn.Name);
        Assert.Equal("https://api.github.com", conn["baseUrl"]);
        Assert.Equal("30", conn["timeout"]);
    }

    [Fact]
    public async Task GetConnection_MixedFields_DereferencesVaultEntries()
    {
        await _vault.InitializeAsync("pw");
        await _vault.AddAsync("github-secrets", "pat", "ghp_xyz");

        _connections.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
            ["token"]   = ConnectionField.OfVault("github-secrets", "pat"),
        }));

        var conn = await _secrets.GetConnectionAsync("github");
        Assert.Equal("https://api.github.com", conn["baseUrl"]);
        Assert.Equal("ghp_xyz", conn["token"]);
    }

    [Fact]
    public void GetConnection_UnknownName_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _secrets.GetConnection("nope"));
    }

    [Fact]
    public async Task GetConnection_HasAndGetOrDefault_BehaveAsExpected()
    {
        _connections.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
        }));

        ConnectionSnapshot conn = (ConnectionSnapshot)await _secrets.GetConnectionAsync("github");
        Assert.True(conn.Has("baseUrl"));
        Assert.False(conn.Has("token"));
        Assert.Equal("https://api.github.com", conn.GetOrDefault("baseUrl", "fallback"));
        Assert.Equal("fallback",                 conn.GetOrDefault("token",   "fallback"));
    }

    [Fact]
    public async Task GetConnection_DynamicMemberAccess_ReturnsField()
    {
        // The script-style call: `var conn = ...; conn.baseUrl;`. Returned as
        // dynamic so the DLR routes member access through ConnectionSnapshot's
        // TryGetMember.
        await _vault.InitializeAsync("pw");
        await _vault.AddAsync("github-secrets", "pat", "ghp_xyz");

        _connections.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
            ["token"]   = ConnectionField.OfVault("github-secrets", "pat"),
        }));

        dynamic conn = await _secrets.GetConnectionAsync("github");
        string url   = conn.baseUrl;
        string token = conn.token;
        Assert.Equal("https://api.github.com", url);
        Assert.Equal("ghp_xyz", token);
    }

    [Fact]
    public async Task GetConnection_DynamicMemberAccess_IsCaseInsensitive()
    {
        _connections.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["BaseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
        }));

        dynamic conn = await _secrets.GetConnectionAsync("github");
        string url = conn.baseurl; // lowercase, falls through case-insensitive
        Assert.Equal("https://api.github.com", url);
    }

    [Fact]
    public async Task GetConnection_Typed_BindsRecordsByName()
    {
        // Strongly-typed binding via the generic overload. The snapshot
        // gets serialized to JSON then deserialized into T, so records
        // with positional ctors and classes with init/settable properties
        // both bind by case-insensitive name match.
        await _vault.InitializeAsync("pw");
        await _vault.AddAsync("github-secrets", "pat", "ghp_xyz");

        _connections.Save(new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
            ["token"]   = ConnectionField.OfVault("github-secrets", "pat"),
        }));

        var typed = await _secrets.GetConnectionAsync<GithubConn>("github");
        Assert.Equal("https://api.github.com", typed.BaseUrl);
        Assert.Equal("ghp_xyz", typed.Token);
    }

    [Fact]
    public async Task GetConnection_Typed_AllowsNumericFieldsFromStrings()
    {
        // Plaintext fields are stored as strings; binding to an int target
        // should still succeed via NumberHandling.AllowReadingFromString.
        _connections.Save(new Connection("api", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://x"),
            ["timeout"] = ConnectionField.Plaintext("30"),
        }));

        var typed = await _secrets.GetConnectionAsync<ApiConn>("api");
        Assert.Equal("https://x", typed.BaseUrl);
        Assert.Equal(30, typed.Timeout);
    }

    private sealed record GithubConn(string BaseUrl, string Token);
    private sealed record ApiConn(string BaseUrl, int Timeout);

    [Fact]
    public void Connections_NoStoreWired_ReturnsEmpty()
    {
        // ScriptSecrets without a connection store still works for legacy
        // all-vault scripts; Connections() should return empty.
        var bare = new ScriptSecrets(_vault, _ => Task.CompletedTask);
        Assert.Empty(bare.Connections());
    }

    private sealed class InMemoryConnectionStore : IConnectionStore
    {
        private readonly Dictionary<string, Connection> _byName = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<Connection> List()
        {
            var list = new List<Connection>(_byName.Values);
            return list;
        }

        public Connection? Get(string name) =>
            string.IsNullOrEmpty(name) ? null : _byName.TryGetValue(name, out var c) ? c : null;

        public void Save(Connection connection) => _byName[connection.Name] = connection;
        public void Remove(string name) => _byName.Remove(name);
        public void Reload() { /* no-op */ }
    }

    private sealed class TestConfigStore : IConfigStore
    {
        public TestConfigStore(string vaultFolder) => VaultFolder = vaultFolder;
        public string ScriptsFolder { get; set; } = "";
        public string FormsFolder   { get; set; } = "";
        public string VaultFolder   { get; set; }
        public string Theme         { get; set; } = "Industrial";
        public void Load() { }
        public void Save() { }
    }
}
