using System;
using System.IO;
using System.Linq;
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
        await _service.InitializeAsync("pw");
        Assert.True(_service.Exists);
        Assert.True(_service.IsUnlocked);

        var entry = await _service.AddAsync("azure", "prod-sql", "Server=foo;", description: "main db");
        Assert.Equal("azure", entry.Category);
        Assert.Equal("prod-sql", entry.Key);
        Assert.Equal("Server=foo;", entry.Value);
        Assert.Equal("main db", entry.Description);

        var all = await _service.ListAsync();
        Assert.Single(all);
        Assert.Equal("Server=foo;", all[0].Value);
    }

    [Fact]
    public async Task Update_ChangesValueAndBumpsUpdatedUtc_ButKeepsId()
    {
        await _service.InitializeAsync("pw");
        var original = await _service.AddAsync("azure", "prod-sql", "v1");

        await Task.Delay(5); // ensure UpdatedUtc advances on platforms with ms precision

        var updated = await _service.UpdateAsync(original.Id, "azure", "prod-sql", "v2", null);

        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("v2", updated.Value);
        Assert.True(updated.UpdatedUtc >= original.UpdatedUtc);

        var all = await _service.ListAsync();
        Assert.Single(all);
        Assert.Equal("v2", all[0].Value);
    }

    [Fact]
    public async Task Update_RenameCategoryAndKey_KeepsIdAndPersists()
    {
        await _service.InitializeAsync("pw");
        var original = await _service.AddAsync("oldcat", "oldkey", "v");

        var updated = await _service.UpdateAsync(original.Id, "newcat", "newkey", "v", null);

        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("newcat", updated.Category);
        Assert.Equal("newkey", updated.Key);

        // Confirm the filename on disk didn't change — the envelope did.
        var secretFiles = Directory.GetFiles(Path.Combine(_config.VaultFolder, "secrets"), "*.secret");
        Assert.Single(secretFiles);
        Assert.Contains(original.Id, Path.GetFileName(secretFiles[0]));
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        await _service.InitializeAsync("pw");
        var e = await _service.AddAsync("c", "k", "v");

        await _service.DeleteAsync(e.Id);

        var all = await _service.ListAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task LockThenUnlock_RestoresAccess()
    {
        await _service.InitializeAsync("pw");
        await _service.AddAsync("c", "k", "v");
        _service.Lock();

        Assert.False(_service.IsUnlocked);
        await _service.UnlockAsync("pw");
        Assert.True(_service.IsUnlocked);

        var all = await _service.ListAsync();
        Assert.Single(all);
        Assert.Equal("v", all[0].Value);
    }

    [Fact]
    public async Task Unlock_WrongPassword_Throws()
    {
        await _service.InitializeAsync("pw");
        _service.Lock();
        await Assert.ThrowsAsync<InvalidMasterPasswordException>(() => _service.UnlockAsync("nope"));
    }

    [Fact]
    public async Task FilenamesOnDisk_AreOpaqueGuids_NoCategoryLeak()
    {
        await _service.InitializeAsync("pw");
        await _service.AddAsync("azure", "prod-sql", "v1");
        await _service.AddAsync("github", "token",    "v2");

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
        await _service.InitializeAsync("pw");
        await _service.AddAsync("Azure", "Prod-SQL", "connstr");

        var value = await _service.ResolveAsync("azure", "prod-sql");
        Assert.Equal("connstr", value);
    }

    [Fact]
    public async Task Resolve_NotFound_Throws()
    {
        await _service.InitializeAsync("pw");
        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(
            () => _service.ResolveAsync("missing", "missing"));
    }

    [Fact]
    public async Task InitializeAsync_Twice_AtSamePath_Throws()
    {
        await _service.InitializeAsync("pw");
        await Assert.ThrowsAsync<VaultAlreadyExistsException>(() => _service.InitializeAsync("pw2"));
    }

    [Fact]
    public async Task Ops_OnLockedVault_Throw()
    {
        await _service.InitializeAsync("pw");
        _service.Lock();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ListAsync());
    }

    // ---------- Destroy ----------

    [Fact]
    public async Task Destroy_RemovesHeaderAndSecretFiles()
    {
        await _service.InitializeAsync("pw");
        await _service.AddAsync("c", "k1", "v1");
        await _service.AddAsync("c", "k2", "v2");

        await _service.DestroyAsync();

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
        await _service.DestroyAsync();
        Assert.False(_service.Exists);
    }

    [Fact]
    public async Task Destroy_ThenInitialize_CreatesFreshVault()
    {
        await _service.InitializeAsync("pw");
        await _service.AddAsync("c", "k", "v");
        await _service.DestroyAsync();

        await _service.InitializeAsync("pw2");
        var all = await _service.ListAsync();
        Assert.Empty(all);
    }

    // ---------- Change password ----------

    [Fact]
    public async Task ChangePassword_OldPasswordFails_NewPasswordWorks()
    {
        await _service.InitializeAsync("old-pw");
        await _service.AddAsync("azure", "prod-sql", "server=foo;");

        await _service.ChangePasswordAsync("new-pw");

        // Service remains unlocked under the new password.
        Assert.True(_service.IsUnlocked);
        _service.Lock();

        await Assert.ThrowsAsync<InvalidMasterPasswordException>(() => _service.UnlockAsync("old-pw"));
        await _service.UnlockAsync("new-pw");

        var all = await _service.ListAsync();
        Assert.Single(all);
        Assert.Equal("server=foo;", all[0].Value);
    }

    [Fact]
    public async Task ChangePassword_PreservesAllEntries()
    {
        await _service.InitializeAsync("old");
        await _service.AddAsync("a", "k1", "v1", "d1");
        await _service.AddAsync("a", "k2", "v2");
        await _service.AddAsync("b", "k3", "v3");

        await _service.ChangePasswordAsync("new");

        var all = (await _service.ListAsync())
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
        await _service.InitializeAsync("old");
        await _service.AddAsync("c", "k", "v");

        await _service.ChangePasswordAsync("new");

        var siblings = Directory.GetFileSystemEntries(_config.VaultFolder)
            .Select(Path.GetFileName)
            .ToList();
        Assert.DoesNotContain(siblings, n => n!.StartsWith("vault.json.bak.", StringComparison.Ordinal));
        Assert.DoesNotContain(siblings, n => n!.StartsWith("secrets.bak.",    StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangePassword_WhenLocked_Throws()
    {
        await _service.InitializeAsync("pw");
        _service.Lock();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ChangePasswordAsync("new"));
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
        public void Load() { }
        public void Save() { }
    }
}
