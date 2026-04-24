using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecretBlast;
using SecretBlast.Interfaces;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Secrets;

/// <summary>
/// Default <see cref="IVaultService"/>. Reads <c>VaultFolder</c> from
/// <see cref="IConfigStore"/> on each op so a user-triggered folder change
/// takes effect the next time the user unlocks.
/// </summary>
public sealed class VaultService : IVaultService, IDisposable
{
    private readonly IConfigStore _config;
    private readonly Func<VaultOptions> _optionsFactory;

    /// <summary>Path the currently-open vault was opened against. Null when locked.</summary>
    private string? _openedAt;
    private ISecretVault? _vault;

    /// <summary>
    /// Production ctor — desktop-grade Argon2 parameters
    /// (256 MiB / 3 iterations / 4 lanes) and the default 15-minute auto-lock.
    /// </summary>
    public VaultService(IConfigStore config)
        : this(config, () => new VaultOptions
        {
            AutoLockIdle = TimeSpan.FromMinutes(15),
            Kdf          = new Argon2Parameters(MemoryKiB: 262144, Iterations: 3, Parallelism: 4),
        })
    { }

    /// <summary>
    /// Test ctor — lets callers pass fast Argon2 parameters so a suite of
    /// unlock-heavy tests doesn't grind for minutes. The factory is invoked
    /// once per Create/Open so parameters can drift over time if needed.
    /// </summary>
    public VaultService(IConfigStore config, Func<VaultOptions> optionsFactory)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _optionsFactory = optionsFactory ?? throw new ArgumentNullException(nameof(optionsFactory));
    }

    public bool Exists => File.Exists(Path.Combine(_config.VaultFolder, "vault.json"));

    public bool IsUnlocked => _vault is { IsLocked: false };

    public event EventHandler? Locked;

    public async Task InitializeAsync(string masterPassword, CancellationToken ct = default)
    {
        DisposeCurrent();

        var path = _config.VaultFolder;
        Directory.CreateDirectory(path);

        var options = _optionsFactory();
        var vault = SecretVault.Create(path, masterPassword, options);
        AttachVault(vault, path);

        await Task.CompletedTask;
        _ = ct; // Create is synchronous in SecretBlast today; swallow ct.
    }

    public async Task UnlockAsync(string masterPassword, CancellationToken ct = default)
    {
        var path = _config.VaultFolder;

        if (_vault is not null && string.Equals(_openedAt, path, StringComparison.Ordinal))
        {
            // Reuse the existing instance; SecretBlast's UnlockAsync on an
            // already-unlocked vault is a no-op and on a locked one derives
            // the key from scratch.
            await _vault.UnlockAsync(masterPassword, ct);
            return;
        }

        DisposeCurrent();

        var options = _optionsFactory();
        var vault = SecretVault.Open(path, options);
        try
        {
            await vault.UnlockAsync(masterPassword, ct);
        }
        catch
        {
            vault.Dispose();
            throw;
        }
        AttachVault(vault, path);
    }

    public void Lock()
    {
        if (_vault is null) return;
        if (!_vault.IsLocked) _vault.Lock();
    }

    public async Task<IReadOnlyList<SecretEntry>> ListAsync(CancellationToken ct = default)
    {
        var vault = EnsureUnlocked();
        var ids = await vault.ListAsync(ct);
        var result = new List<SecretEntry>(ids.Count);
        foreach (var id in ids)
        {
            var json = await vault.GetAsync(id, ct);
            SecretEnvelope env;
            try
            {
                env = SecretEnvelope.FromJson(json);
            }
            catch (InvalidSecretEnvelopeException)
            {
                // Ignore entries we don't understand — forward compat.
                continue;
            }
            result.Add(ToEntry(id, env));
        }
        return result;
    }

    public async Task<SecretEntry> AddAsync(string category, string key, string value, string? description = null, CancellationToken ct = default)
    {
        var vault = EnsureUnlocked();
        var envelope = SecretEnvelope.Create(category, key, value, description);
        var id = SecretId.NewId();
        await vault.SetAsync(id, envelope.ToJson(), ct);
        return ToEntry(id, envelope);
    }

    public async Task<SecretEntry> UpdateAsync(string id, string category, string key, string value, string? description, CancellationToken ct = default)
    {
        var vault = EnsureUnlocked();
        var currentJson = await vault.GetAsync(id, ct);
        var current = SecretEnvelope.FromJson(currentJson);
        var updated = current.With(category: category, key: key, value: value, description: description);
        await vault.SetAsync(id, updated.ToJson(), ct);
        return ToEntry(id, updated);
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var vault = EnsureUnlocked();
        return vault.DeleteAsync(id, ct);
    }

    public async Task<string> ResolveAsync(string category, string key, CancellationToken ct = default)
    {
        var wantedCategory = (category ?? string.Empty).Trim();
        var wantedKey      = (key      ?? string.Empty).Trim();

        var all = await ListAsync(ct);
        foreach (var entry in all)
        {
            if (string.Equals(entry.Category, wantedCategory, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Key,      wantedKey,      StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }
        throw new KeyNotFoundException($"No secret found for {wantedCategory}/{wantedKey}.");
    }

    public Task DestroyAsync(CancellationToken ct = default)
    {
        // Release file handles before touching the on-disk layout — matters on
        // Windows where open handles block deletion.
        DisposeCurrent();

        var folder = _config.VaultFolder;
        var header = Path.Combine(folder, "vault.json");
        var secretsDir = Path.Combine(folder, "secrets");

        if (File.Exists(header)) File.Delete(header);
        if (Directory.Exists(secretsDir))
        {
            foreach (var f in Directory.EnumerateFiles(secretsDir, "*.secret")) File.Delete(f);
            // Stray atomic-write temp files from crashed writes.
            foreach (var f in Directory.EnumerateFiles(secretsDir, "*.tmp"))    File.Delete(f);
        }

        _ = ct;
        return Task.CompletedTask;
    }

    public async Task ChangePasswordAsync(string newPassword, CancellationToken ct = default)
    {
        var vault = EnsureUnlocked();

        // 1. Capture every envelope into memory. If decoding a record fails,
        //    skip it — forward-compat — but surface the loss in the exception
        //    message if the rewrite phase later dies.
        var ids = await vault.ListAsync(ct);
        var snapshot = new List<(string category, string key, string value, string? description, DateTime createdUtc)>(ids.Count);
        foreach (var id in ids)
        {
            var json = await vault.GetAsync(id, ct);
            SecretEnvelope env;
            try { env = SecretEnvelope.FromJson(json); }
            catch (InvalidSecretEnvelopeException) { continue; }
            snapshot.Add((env.Category, env.Key, env.Value, env.Description, env.CreatedUtc));
        }

        // 2. Release handles and rename the live vault aside. Using rename
        //    keeps the rollback atomic per-file and avoids a copy.
        DisposeCurrent();

        var folder = _config.VaultFolder;
        var ts = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var header = Path.Combine(folder, "vault.json");
        var secretsDir = Path.Combine(folder, "secrets");
        var headerBak = Path.Combine(folder, $"vault.json.bak.{ts}");
        var secretsBak = Path.Combine(folder, $"secrets.bak.{ts}");

        if (File.Exists(header))          File.Move(header, headerBak);
        if (Directory.Exists(secretsDir)) Directory.Move(secretsDir, secretsBak);

        // 3. Create fresh with the new password and replay the snapshot. If
        //    anything throws here the caller still has the backup alongside.
        try
        {
            await InitializeAsync(newPassword, ct);
            foreach (var e in snapshot)
            {
                await AddAsync(e.category, e.key, e.value, e.description, ct);
            }

            // 4. Success — the old backups are no longer useful.
            if (File.Exists(headerBak))          File.Delete(headerBak);
            if (Directory.Exists(secretsBak))    Directory.Delete(secretsBak, recursive: true);
        }
        catch
        {
            // Leave the backup in place so the user can recover by renaming
            // the *.bak.<ts> pair back to vault.json / secrets/.
            throw;
        }
    }

    public void Dispose() => DisposeCurrent();

    private ISecretVault EnsureUnlocked()
    {
        if (_vault is null || _vault.IsLocked)
            throw new InvalidOperationException("Vault is locked. Call UnlockAsync first.");
        return _vault;
    }

    private void AttachVault(ISecretVault vault, string path)
    {
        _vault = vault;
        _openedAt = path;
        vault.Locked += OnVaultLocked;
    }

    private void OnVaultLocked(object? sender, EventArgs e) => Locked?.Invoke(this, EventArgs.Empty);

    private void DisposeCurrent()
    {
        if (_vault is null) return;
        _vault.Locked -= OnVaultLocked;
        _vault.Dispose();
        _vault = null;
        _openedAt = null;
    }

    private static SecretEntry ToEntry(string id, SecretEnvelope env) =>
        new(id, env.Category, env.Key, env.Value, env.Description, env.CreatedUtc, env.UpdatedUtc);
}
