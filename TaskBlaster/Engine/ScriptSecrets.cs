using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Connections;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Engine;

/// <summary>
/// Script-facing wrapper over <see cref="IVaultService"/>. Exposed to
/// .csx scripts as <c>Secrets</c> via <see cref="ScriptGlobals"/>.
///
/// Scripts typically call the sync form — <c>Secrets.Resolve("azure", "prod-sql")</c>
/// — because .csx bodies read top-to-bottom. Blocking on the current
/// thread is safe: <see cref="ScriptBlaster"/> runs every script off the
/// UI thread inside <see cref="Task.Run(Func{Task})"/>.
///
/// Libraries that want a named-connection resolver (AzureBlast, the
/// planned NetBlast, etc.) accept a <see cref="Resolver"/> delegate
/// rather than a <c>ScriptSecrets</c> reference — the library stays
/// free of any TaskBlaster / SecretBlast dependency.
/// </summary>
public sealed class ScriptSecrets
{
    private readonly IVaultService _vault;
    private readonly Func<CancellationToken, Task> _ensureUnlocked;
    private readonly Func<string, string, CancellationToken, Task<string>> _resolver;
    private readonly IConnectionStore? _connections;

    public ScriptSecrets(
        IVaultService vault,
        Func<CancellationToken, Task> ensureUnlocked)
        : this(vault, ensureUnlocked, connections: null) { }

    /// <summary>
    /// Three-arg ctor used by the host app to layer in the
    /// <see cref="IConnectionStore"/>. When non-null, plaintext fields
    /// in the connections file short-circuit the vault unlock entirely;
    /// vault-ref fields dispatch to <see cref="IVaultService.ResolveAsync"/>
    /// after the standard unlock-on-demand handshake.
    /// </summary>
    public ScriptSecrets(
        IVaultService vault,
        Func<CancellationToken, Task> ensureUnlocked,
        IConnectionStore? connections)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _ensureUnlocked = ensureUnlocked ?? throw new ArgumentNullException(nameof(ensureUnlocked));
        _connections = connections;

        if (connections is null)
        {
            _resolver = ResolveFromVaultAsync;
        }
        else
        {
            var wrapped = new ConnectionsResolver(connections, ResolveFromVaultAsync);
            _resolver = wrapped.ResolveAsync;
        }
    }

    /// <summary>
    /// Resolve a (category, key) pair to its raw value. Blocks until the
    /// vault is unlocked (when needed) and the lookup completes. Throws
    /// <see cref="InvalidOperationException"/> if the vault remains
    /// locked (e.g. the user cancelled the unlock prompt). Plaintext
    /// fields from the connections file return without unlocking.
    /// </summary>
    public string Resolve(string category, string key)
        => ResolveAsync(category, key).GetAwaiter().GetResult();

    /// <summary>Async form of <see cref="Resolve"/>.</summary>
    public Task<string> ResolveAsync(string category, string key, CancellationToken ct = default)
        => _resolver(category, key, ct);

    /// <summary>
    /// Delegate form of <see cref="ResolveAsync"/>, shaped for libraries
    /// that accept a named-connection resolver:
    /// <code>
    /// // AzureBlast 2.1+ — two-step (build, then SetupAsync):
    /// var db = new MssqlDatabase();
    /// await db.SetupAsync(Secrets.Resolver, "prod-sql");
    ///
    /// // NetworkBlast 1.0+ — one-step (resolver baked into ctor):
    /// var api = new NetClient(Secrets.Resolver, "github");
    /// </code>
    /// </summary>
    public Func<string, string, CancellationToken, Task<string>> Resolver => _resolver;

    private async Task<string> ResolveFromVaultAsync(string category, string key, CancellationToken ct)
    {
        await _ensureUnlocked(ct).ConfigureAwait(false);
        if (!_vault.IsUnlocked)
            throw new VaultLockedException("Vault is locked, cannot resolve secret.");
        return await _vault.ResolveAsync(category, key, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Connection names defined in <c>connections.json</c>. Empty when
    /// no connections file exists or no connections are registered.
    /// Useful for building a quick picker (e.g.
    /// <c>Prompts.Select("Connection", "…", Secrets.Connections())</c>).
    /// </summary>
    public IReadOnlyList<string> Connections()
        => _connections is null
            ? Array.Empty<string>()
            : _connections.List().Select(c => c.Name).ToList();

    /// <summary>
    /// Resolve every field of a named connection in one call. Plaintext
    /// fields return their literal; vault-ref fields go through
    /// <see cref="ResolveAsync"/>, unlocking the vault on demand. Throws
    /// <see cref="KeyNotFoundException"/> if the connection isn't defined,
    /// or <see cref="VaultLockedException"/> if a vault-ref field is
    /// needed and the user cancels the unlock prompt.
    /// <para>
    /// The return type is <c>dynamic</c> so a script can write
    /// <c>var conn = Secrets.GetConnection("github"); var url = conn.baseUrl;</c>
    /// directly. The runtime object is a <see cref="ConnectionSnapshot"/>
    /// (a <see cref="System.Dynamic.DynamicObject"/>), so typed access
    /// like <c>conn["baseUrl"]</c>, <c>conn.GetOrDefault(...)</c> and
    /// <c>conn.Fields</c> works too.
    /// </para>
    /// </summary>
    public dynamic GetConnection(string name)
        => GetConnectionAsync(name).GetAwaiter().GetResult();

    /// <summary>Async form of <see cref="GetConnection"/>.</summary>
    public async Task<dynamic> GetConnectionAsync(string name, CancellationToken ct = default)
        => await GetConnectionSnapshotAsync(name, ct).ConfigureAwait(false);

    /// <summary>
    /// Strongly-typed binding of a named connection to <typeparamref name="T"/>.
    /// Field names are matched to <typeparamref name="T"/> property /
    /// constructor-parameter names case-insensitively. Records and
    /// classes with init or settable properties both work; numeric
    /// targets read string values via
    /// <see cref="JsonNumberHandling.AllowReadingFromString"/>. Useful
    /// when a script wants compile-time field names rather than dynamic
    /// dispatch:
    /// <code>
    /// record GithubConn(string baseUrl, string token);
    /// var c = Secrets.GetConnection&lt;GithubConn&gt;("github");
    /// // c.baseUrl, c.token — IntelliSense + null-checks.
    /// </code>
    /// </summary>
    public T GetConnection<T>(string name) where T : class
        => GetConnectionAsync<T>(name).GetAwaiter().GetResult();

    /// <summary>Async form of <see cref="GetConnection{T}"/>.</summary>
    public async Task<T> GetConnectionAsync<T>(string name, CancellationToken ct = default) where T : class
    {
        var snapshot = await GetConnectionSnapshotAsync(name, ct).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(snapshot.Fields);
        var typed = JsonSerializer.Deserialize<T>(json, TypedConnectionOptions);
        return typed
            ?? throw new InvalidOperationException(
                $"Could not bind connection '{name}' to {typeof(T).Name}.");
    }

    private static readonly JsonSerializerOptions TypedConnectionOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private async Task<ConnectionSnapshot> GetConnectionSnapshotAsync(string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connection name is required.", nameof(name));

        var conn = _connections?.Get(name)
            ?? throw new KeyNotFoundException($"No connection named '{name}'.");

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (fieldName, _) in conn.Fields)
        {
            // Go through the wrapped resolver so plaintext stays plaintext
            // and vault refs trigger exactly one unlock-on-demand handshake.
            resolved[fieldName] = await _resolver(name, fieldName, ct).ConfigureAwait(false);
        }
        return new ConnectionSnapshot(name, resolved);
    }

    /// <summary>
    /// Category names the vault knows about. Useful for building form
    /// dropdowns (e.g. <c>Prompts.Select("Category", "…", Secrets.Categories())</c>).
    /// Values are never included.
    /// </summary>
    public IReadOnlyList<string> Categories()
        => CategoriesAsync().GetAwaiter().GetResult();

    /// <summary>Async form of <see cref="Categories"/>.</summary>
    public async Task<IReadOnlyList<string>> CategoriesAsync(CancellationToken ct = default)
    {
        await _ensureUnlocked(ct).ConfigureAwait(false);
        if (!_vault.IsUnlocked)
            throw new VaultLockedException("Vault is locked, cannot list categories.");
        return await _vault.GetCategoriesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Keys stored under <paramref name="category"/>, case-insensitive.
    /// Returns an empty list if the category exists but holds nothing.
    /// Values are never included — this is safe to hand to a form as the
    /// options list for a select field.
    /// </summary>
    public IReadOnlyList<string> Keys(string category)
        => KeysAsync(category).GetAwaiter().GetResult();

    /// <summary>Async form of <see cref="Keys"/>.</summary>
    public async Task<IReadOnlyList<string>> KeysAsync(string category, CancellationToken ct = default)
    {
        await _ensureUnlocked(ct).ConfigureAwait(false);
        if (!_vault.IsUnlocked)
            throw new VaultLockedException("Vault is locked, cannot list keys.");

        var wanted = (category ?? string.Empty).Trim();
        var all = await _vault.ListAsync(ct).ConfigureAwait(false);
        return all
            .Where(e => string.Equals(e.Category, wanted, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Key)
            .ToList();
    }
}
