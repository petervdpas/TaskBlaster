using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SecretBlast;
using TaskBlaster.Interfaces;
using TaskBlaster.Secrets;

namespace TaskBlaster.Tests;

/// <summary>
/// End-to-end tests for <see cref="VaultService"/> — exercise the
/// SecretBlast round-trip through the envelope codec. Argon2 is costly at
/// production parameters, so we override the KDF to the same
/// <c>Argon2Parameters(1024, 1, 1)</c> value SecretBlast's own fast-test
/// helper uses so the whole suite completes in under a second.
/// </summary>
public sealed class VaultServiceTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _root;
    private readonly TestConfigStore _config;
    private readonly VaultService _service;

    public VaultServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tb-vs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _config = new TestConfigStore(Path.Combine(_root, "vault"));
        _service = new VaultService(_config, () => new VaultOptions
        {
            // Fast Argon2 params for tests — keeps the whole suite sub-second.
            AutoLockIdle = System.Threading.Timeout.InfiniteTimeSpan,
            Kdf          = new Argon2Parameters(MemoryKiB: 1024, Iterations: 1, Parallelism: 1),
        });
    }

    public void Dispose()
    {
        _service.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Exists_IsFalse_UntilInitialized()
    {
        Assert.False(_service.Exists);
        Assert.False(_service.IsUnlocked);
    }

    [Fact]
    public async Task Initialize_ThenAdd_ThenList_RoundTrips()
    {
        await _service.InitializeAsync("pw", Ct);
        Assert.True(_service.Exists);
        Assert.True(_service.IsUnlocked);

        var entry = await _service.AddAsync("azure", "prod-sql", "Server=foo;", description: "main db", ct: Ct);
        Assert.Equal("azure", entry.Category);
        Assert.Equal("prod-sql", entry.Key);
        Assert.Equal("Server=foo;", entry.Value);
        Assert.Equal("main db", entry.Description);

        var all = await _service.ListAsync(Ct);
        Assert.Single(all);
        Assert.Equal("Server=foo;", all[0].Value);
    }

    [Fact]
    public async Task Update_ChangesValueAndBumpsUpdatedUtc_ButKeepsId()
    {
        await _service.InitializeAsync("pw", Ct);
        var original = await _service.AddAsync("azure", "prod-sql", "v1", ct: Ct);

        await Task.Delay(5, Ct); // ensure UpdatedUtc advances on platforms with ms precision

        var updated = await _service.UpdateAsync(original.Id, "azure", "prod-sql", "v2", null, Ct);

        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("v2", updated.Value);
        Assert.True(updated.UpdatedUtc >= original.UpdatedUtc);

        var all = await _service.ListAsync(Ct);
        Assert.Single(all);
        Assert.Equal("v2", all[0].Value);
    }

    [Fact]
    public async Task Update_RenameCategoryAndKey_KeepsIdAndPersists()
    {
        await _service.InitializeAsync("pw", Ct);
        var original = await _service.AddAsync("oldcat", "oldkey", "v", ct: Ct);

        var updated = await _service.UpdateAsync(original.Id, "newcat", "newkey", "v", null, Ct);

        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("newcat", updated.Category);
        Assert.Equal("newkey", updated.Key);

        // Confirm the filename on disk didn't change — the envelope did.
        var secretFiles = Directory.GetFiles(Path.Combine(_config.VaultFolder, "secrets"), "*.secret");
        Assert.Single(secretFiles);
        Assert.Contains(original.Id, Path.GetFileName(secretFiles[0]));
    }

    [Fact]
    public async Task RenameCategory_RewritesMatchingEnvelopes_KeepsIds_AndIsCaseInsensitiveOnMatch()
    {
        await _service.InitializeAsync("pw", Ct);
        var a = await _service.AddAsync("Azure",  "k1", "v1", ct: Ct);
        var b = await _service.AddAsync("azure",  "k2", "v2", ct: Ct);
        var c = await _service.AddAsync("github", "k3", "v3", ct: Ct);

        var rewritten = await _service.RenameCategoryAsync("azure", "Cloud", Ct);

        Assert.Equal(2, rewritten);

        var all = (await _service.ListAsync(Ct)).OrderBy(e => e.Key).ToList();
        Assert.Equal("Cloud",  all[0].Category);
        Assert.Equal("Cloud",  all[1].Category);
        Assert.Equal("github", all[2].Category);

        // Same ids → same on-disk filenames.
        var byId = all.ToDictionary(e => e.Id);
        Assert.True(byId.ContainsKey(a.Id));
        Assert.True(byId.ContainsKey(b.Id));
        Assert.True(byId.ContainsKey(c.Id));
    }

    [Fact]
    public async Task RenameCategory_NoMatches_ReturnsZero_AndDoesNotTouchEnvelopes()
    {
        await _service.InitializeAsync("pw", Ct);
        var e = await _service.AddAsync("github", "tok", "v", ct: Ct);
        var beforeUpdated = e.UpdatedUtc;

        var rewritten = await _service.RenameCategoryAsync("nothere", "Cloud", Ct);
        Assert.Equal(0, rewritten);

        var all = await _service.ListAsync(Ct);
        Assert.Single(all);
        Assert.Equal("github", all[0].Category);
        Assert.Equal(beforeUpdated, all[0].UpdatedUtc);
    }

    [Fact]
    public async Task RenameCategory_SameNameNoOp_ReturnsZero()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.AddAsync("Azure", "k", "v", ct: Ct);

        var rewritten = await _service.RenameCategoryAsync("Azure", "Azure", Ct);
        Assert.Equal(0, rewritten);
    }

    [Fact]
    public async Task RenameCategory_LeavesCatalogUntouched_SoCallerStillCommitsIt()
    {
        // RenameCategoryAsync only rewrites secret envelopes; the picker-list
        // catalog is the caller's responsibility (paired with SetCategoriesAsync).
        // After a rename without a follow-up SetCategoriesAsync, the union view
        // surfaces BOTH the stale catalog entry and the new category that
        // secrets actually use, which is the signal that the caller still
        // needs to commit the new list.
        await _service.InitializeAsync("pw", Ct);
        await _service.SetCategoriesAsync(new[] { "Azure", "github" }, Ct);
        await _service.AddAsync("Azure", "k", "v", ct: Ct);

        await _service.RenameCategoryAsync("Azure", "Cloud", Ct);

        var cats = await _service.GetCategoriesAsync(Ct);
        Assert.Contains("Azure", cats); // catalog still has the old name
        Assert.Contains("Cloud", cats); // secrets now use the new name
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        await _service.InitializeAsync("pw", Ct);
        var e = await _service.AddAsync("c", "k", "v", ct: Ct);

        await _service.DeleteAsync(e.Id, Ct);

        var all = await _service.ListAsync(Ct);
        Assert.Empty(all);
    }

    [Fact]
    public async Task LockThenUnlock_RestoresAccess()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.AddAsync("c", "k", "v", ct: Ct);
        _service.Lock();

        Assert.False(_service.IsUnlocked);
        await _service.UnlockAsync("pw", Ct);
        Assert.True(_service.IsUnlocked);

        var all = await _service.ListAsync(Ct);
        Assert.Single(all);
        Assert.Equal("v", all[0].Value);
    }

    [Fact]
    public async Task Unlock_WrongPassword_Throws()
    {
        await _service.InitializeAsync("pw", Ct);
        _service.Lock();
        await Assert.ThrowsAsync<InvalidMasterPasswordException>(() => _service.UnlockAsync("nope", Ct));
    }

    [Fact]
    public async Task FilenamesOnDisk_AreOpaqueGuids_NoCategoryLeak()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.AddAsync("azure", "prod-sql", "v1", ct: Ct);
        await _service.AddAsync("github", "token",    "v2", ct: Ct);

        var files = Directory.GetFiles(Path.Combine(_config.VaultFolder, "secrets"), "*.secret");
        Assert.Equal(2, files.Length);
        foreach (var f in files)
        {
            var name = Path.GetFileNameWithoutExtension(f);
            Assert.Equal(32, name.Length);
            Assert.DoesNotContain("azure",    name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("github",   name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("prod-sql", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Resolve_ByCategoryAndKey_IsCaseInsensitive()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.AddAsync("Azure", "Prod-SQL", "connstr", ct: Ct);

        var value = await _service.ResolveAsync("azure", "prod-sql", Ct);
        Assert.Equal("connstr", value);
    }

    [Fact]
    public async Task Resolve_NotFound_Throws()
    {
        await _service.InitializeAsync("pw", Ct);
        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(
            () => _service.ResolveAsync("missing", "missing", Ct));
    }

    [Fact]
    public async Task InitializeAsync_Twice_AtSamePath_Throws()
    {
        await _service.InitializeAsync("pw", Ct);
        await Assert.ThrowsAsync<VaultAlreadyExistsException>(() => _service.InitializeAsync("pw2", Ct));
    }

    [Fact]
    public async Task Ops_OnLockedVault_Throw()
    {
        await _service.InitializeAsync("pw", Ct);
        _service.Lock();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ListAsync(Ct));
    }

    // ---------- Destroy ----------

    [Fact]
    public async Task Destroy_RemovesHeaderAndSecretFiles()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.AddAsync("c", "k1", "v1", ct: Ct);
        await _service.AddAsync("c", "k2", "v2", ct: Ct);

        await _service.DestroyAsync(Ct);

        Assert.False(_service.Exists);
        Assert.False(_service.IsUnlocked);
        Assert.False(File.Exists(Path.Combine(_config.VaultFolder, "vault.json")));
        var secretsDir = Path.Combine(_config.VaultFolder, "secrets");
        if (Directory.Exists(secretsDir))
            Assert.Empty(Directory.GetFiles(secretsDir, "*.secret"));
    }

    [Fact]
    public async Task Destroy_WhenNoVaultExists_IsNoOp()
    {
        Assert.False(_service.Exists);
        await _service.DestroyAsync(Ct);
        Assert.False(_service.Exists);
    }

    [Fact]
    public async Task Destroy_ThenInitialize_CreatesFreshVault()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.AddAsync("c", "k", "v", ct: Ct);
        await _service.DestroyAsync(Ct);

        await _service.InitializeAsync("pw2", Ct);
        var all = await _service.ListAsync(Ct);
        Assert.Empty(all);
    }

    // ---------- Change password ----------

    [Fact]
    public async Task ChangePassword_OldPasswordFails_NewPasswordWorks()
    {
        await _service.InitializeAsync("old-pw", Ct);
        await _service.AddAsync("azure", "prod-sql", "server=foo;", ct: Ct);

        await _service.ChangePasswordAsync("new-pw", Ct);

        // Service remains unlocked under the new password.
        Assert.True(_service.IsUnlocked);
        _service.Lock();

        await Assert.ThrowsAsync<InvalidMasterPasswordException>(() => _service.UnlockAsync("old-pw", Ct));
        await _service.UnlockAsync("new-pw", Ct);

        var all = await _service.ListAsync(Ct);
        Assert.Single(all);
        Assert.Equal("server=foo;", all[0].Value);
    }

    [Fact]
    public async Task ChangePassword_PreservesAllEntries()
    {
        await _service.InitializeAsync("old", Ct);
        await _service.AddAsync("a", "k1", "v1", "d1", Ct);
        await _service.AddAsync("a", "k2", "v2", ct: Ct);
        await _service.AddAsync("b", "k3", "v3", ct: Ct);

        await _service.ChangePasswordAsync("new", Ct);

        var all = (await _service.ListAsync(Ct))
            .OrderBy(e => e.Category).ThenBy(e => e.Key)
            .ToList();
        Assert.Equal(3, all.Count);
        Assert.Equal(("a", "k1", "v1", "d1"), (all[0].Category, all[0].Key, all[0].Value, all[0].Description));
        Assert.Equal(("a", "k2", "v2", (string?)null), (all[1].Category, all[1].Key, all[1].Value, all[1].Description));
        Assert.Equal(("b", "k3", "v3", (string?)null), (all[2].Category, all[2].Key, all[2].Value, all[2].Description));
    }

    [Fact]
    public async Task ChangePassword_Success_RemovesBackupFiles()
    {
        await _service.InitializeAsync("old", Ct);
        await _service.AddAsync("c", "k", "v", ct: Ct);

        await _service.ChangePasswordAsync("new", Ct);

        var siblings = Directory.GetFileSystemEntries(_config.VaultFolder)
            .Select(Path.GetFileName)
            .ToList();
        Assert.DoesNotContain(siblings, n => n!.StartsWith("vault.json.bak.", StringComparison.Ordinal));
        Assert.DoesNotContain(siblings, n => n!.StartsWith("secrets.bak.",    StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangePassword_WhenLocked_Throws()
    {
        await _service.InitializeAsync("pw", Ct);
        _service.Lock();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ChangePasswordAsync("new", Ct));
    }

    // ---------- Categories catalog ----------

    [Fact]
    public async Task GetCategories_FreshVault_IsEmpty()
    {
        await _service.InitializeAsync("pw", Ct);
        var cats = await _service.GetCategoriesAsync(Ct);
        Assert.Empty(cats);
    }

    [Fact]
    public async Task SetCategories_ThenGet_ReturnsSortedNormalizedList()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.SetCategoriesAsync(new[] { "github", "  azure  ", "GITHUB", "", "backup" }, Ct);

        var cats = await _service.GetCategoriesAsync(Ct);
        // Normalize: trim, dedupe case-insensitively (first-seen casing wins), drop empties, sort.
        Assert.Equal(new[] { "azure", "backup", "github" }, cats.ToArray());
    }

    [Fact]
    public async Task ListAsync_DoesNotLeakCatalogAsSecret()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.SetCategoriesAsync(new[] { "azure" }, Ct);
        await _service.AddAsync("azure", "prod-sql", "v", ct: Ct);

        var all = await _service.ListAsync(Ct);
        Assert.Single(all);
        Assert.Equal("azure", all[0].Category);
        Assert.Equal("prod-sql", all[0].Key);
    }

    [Fact]
    public async Task GetCategories_FirstRead_MigratesFromLiveSecrets()
    {
        // Simulate a pre-catalog vault by adding secrets before any catalog write.
        await _service.InitializeAsync("pw", Ct);
        await _service.AddAsync("azure",  "prod-sql", "v1", ct: Ct);
        await _service.AddAsync("github", "token",    "v2", ct: Ct);

        var cats = await _service.GetCategoriesAsync(Ct);
        Assert.Equal(new[] { "azure", "github" }, cats.ToArray());
    }

    [Fact]
    public async Task GetCategories_FirstRead_PersistsMigration()
    {
        // After migration, a fresh lock/unlock round trip must return the same
        // list *without* needing another migration (proves we wrote it).
        await _service.InitializeAsync("pw", Ct);
        await _service.AddAsync("azure", "prod-sql", "v", ct: Ct);
        _ = await _service.GetCategoriesAsync(Ct);   // migration
        _service.Lock();
        await _service.UnlockAsync("pw", Ct);

        // If the write didn't take, deleting the only secret would leave
        // GetCategoriesAsync returning []. After a real persist, the category
        // survives removal of its last secret.
        var onlyEntry = (await _service.ListAsync(Ct)).Single();
        await _service.DeleteAsync(onlyEntry.Id, Ct);

        var cats = await _service.GetCategoriesAsync(Ct);
        Assert.Contains("azure", cats);
    }

    [Fact]
    public async Task ChangePassword_PreservesCategoryCatalog()
    {
        await _service.InitializeAsync("old", Ct);
        await _service.SetCategoriesAsync(new[] { "azure", "github", "backup" }, Ct);
        await _service.AddAsync("azure", "prod", "v", ct: Ct);

        await _service.ChangePasswordAsync("new", Ct);

        var cats = await _service.GetCategoriesAsync(Ct);
        Assert.Equal(new[] { "azure", "backup", "github" }, cats.ToArray());
    }

    [Fact]
    public async Task Categories_SurviveLockUnlockCycle()
    {
        await _service.InitializeAsync("pw", Ct);
        await _service.SetCategoriesAsync(new[] { "azure", "github" }, Ct);

        _service.Lock();
        await _service.UnlockAsync("pw", Ct);

        var cats = await _service.GetCategoriesAsync(Ct);
        Assert.Equal(new[] { "azure", "github" }, cats.ToArray());
    }

    /// <summary>
    /// Minimal <see cref="IConfigStore"/> for tests — only <see cref="VaultFolder"/>
    /// matters for <see cref="VaultService"/>; the other paths are unused.
    /// </summary>
    private sealed class TestConfigStore : IConfigStore
    {
        public TestConfigStore(string vaultFolder) => VaultFolder = vaultFolder;
        public string ScriptsFolder { get; set; } = "";
        public string FormsFolder   { get; set; } = "";
        public string VaultFolder   { get; set; }
        public string Theme         { get; set; } = "Industrial";
        public bool TerminalVisible { get; set; } = true;
        public string EditorHighlighter { get; set; } = "Native";
        public bool CodeFolding { get; set; } = true;
        public System.Collections.Generic.IList<string> ExternalDlls { get; }
            = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.IList<TaskBlaster.Externals.ExternalPackageRef> ExternalPackages { get; }
            = new System.Collections.Generic.List<TaskBlaster.Externals.ExternalPackageRef>();
        public void Load() { }
        public void Save() { }
    }
}
