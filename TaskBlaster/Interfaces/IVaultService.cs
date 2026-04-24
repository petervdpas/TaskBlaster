using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Secrets;

namespace TaskBlaster.Interfaces;

/// <summary>
/// Stateful wrapper around SecretBlast that presents a category/key/value
/// model to TaskBlaster. Opaque GUIDs are used as SecretBlast secret names;
/// the real metadata lives inside a JSON envelope encrypted at rest.
///
/// The service is deliberately prompt-free: the UI layer owns password
/// dialogs and calls <see cref="InitializeAsync"/> / <see cref="UnlockAsync"/>
/// with the entered password.
/// </summary>
public interface IVaultService
{
    /// <summary>True when a vault.json exists under the configured vault folder.</summary>
    bool Exists { get; }

    /// <summary>True when the vault is open and unlocked and ready for data ops.</summary>
    bool IsUnlocked { get; }

    /// <summary>Fires when the vault transitions locked → any state requiring re-unlock.</summary>
    event EventHandler? Locked;

    /// <summary>Create a new vault with the given master password. Fails if one already exists.</summary>
    Task InitializeAsync(string masterPassword, CancellationToken ct = default);

    /// <summary>
    /// Open + unlock an existing vault. Throws <c>InvalidMasterPasswordException</c>
    /// (from SecretBlast) on wrong password; <c>VaultNotFoundException</c> if no
    /// vault exists at the configured path.
    /// </summary>
    Task UnlockAsync(string masterPassword, CancellationToken ct = default);

    /// <summary>Lock and zero the master key. Safe to call when already locked.</summary>
    void Lock();

    Task<IReadOnlyList<SecretEntry>> ListAsync(CancellationToken ct = default);
    Task<SecretEntry> AddAsync   (string category, string key, string value, string? description = null, CancellationToken ct = default);
    Task<SecretEntry> UpdateAsync(string id, string category, string key, string value, string? description, CancellationToken ct = default);
    Task              DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Resolve a (category, key) pair to its raw value. Used by integrations
    /// (e.g. AzureBlast named-connection resolver). Throws if no matching
    /// entry exists.
    /// </summary>
    Task<string> ResolveAsync(string category, string key, CancellationToken ct = default);

    /// <summary>
    /// Permanently delete the vault at the configured folder — <c>vault.json</c>
    /// plus every <c>*.secret</c>. Any unlocked state is cleared first. Safe to
    /// call when no vault exists; safe to call when locked. After this, the
    /// next <see cref="UnlockAsync"/> would fail and the next
    /// <see cref="InitializeAsync"/> creates a fresh vault.
    /// </summary>
    Task DestroyAsync(CancellationToken ct = default);

    /// <summary>
    /// Re-key the vault with a new master password. Requires the vault to be
    /// unlocked (the caller has already proven the old password). All entries
    /// are read into memory, the on-disk vault is renamed aside as
    /// <c>*.bak.&lt;timestamp&gt;</c>, a fresh vault is created under the new
    /// password, and every entry is re-inserted. On success the backup is
    /// removed; on failure the backup is left in place for recovery and the
    /// exception is rethrown.
    /// </summary>
    Task ChangePasswordAsync(string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Load the user-configured category list. On first read — before the
    /// user has ever opened the Categories dialog — the returned list is the
    /// union of (any previously persisted catalog) and every category
    /// actually used by an existing secret. That migration step means
    /// pre-category-catalog vaults don't appear empty.
    /// </summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>Persist a replacement category list.</summary>
    Task SetCategoriesAsync(IEnumerable<string> categories, CancellationToken ct = default);
}
