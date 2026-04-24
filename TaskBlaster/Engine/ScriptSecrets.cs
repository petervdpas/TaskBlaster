using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public ScriptSecrets(
        IVaultService vault,
        Func<CancellationToken, Task> ensureUnlocked)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _ensureUnlocked = ensureUnlocked ?? throw new ArgumentNullException(nameof(ensureUnlocked));
    }

    /// <summary>
    /// Resolve a (category, key) pair to its raw value. Blocks until the
    /// vault is unlocked and the lookup completes. Throws
    /// <see cref="InvalidOperationException"/> if the vault remains
    /// locked (e.g. the user cancelled the unlock prompt).
    /// </summary>
    public string Resolve(string category, string key)
        => ResolveAsync(category, key).GetAwaiter().GetResult();

    /// <summary>Async form of <see cref="Resolve"/>.</summary>
    public async Task<string> ResolveAsync(string category, string key, CancellationToken ct = default)
    {
        await _ensureUnlocked(ct).ConfigureAwait(false);
        if (!_vault.IsUnlocked)
            throw new VaultLockedException("Vault is locked, cannot resolve secret.");
        return await _vault.ResolveAsync(category, key, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delegate form of <see cref="ResolveAsync"/>, shaped for libraries
    /// that accept a named-connection resolver:
    /// <code>
    /// // Hypothetical AzureBlast / NetBlast call sites:
    /// var db  = new MssqlDatabase(Secrets.Resolver, "prod-sql");
    /// var api = new NetClient(Secrets.Resolver, "github-token");
    /// </code>
    /// </summary>
    public Func<string, string, CancellationToken, Task<string>> Resolver => ResolveAsync;

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
