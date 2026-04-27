using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SecretBlast;
using TaskBlaster.Engine;
using TaskBlaster.Interfaces;
using TaskBlaster.Secrets;

namespace TaskBlaster.Tests;

/// <summary>
/// End-to-end tests for <see cref="ScriptGlobals"/> / <see cref="ScriptSecrets"/>:
/// run a real <c>.csx</c> through <see cref="ScriptBlaster"/> with a real
/// <see cref="VaultService"/> behind the globals, and assert that the
/// script can resolve a secret via <c>Secrets.Resolve</c>.
/// </summary>
[Collection("ScriptBlaster")]
public sealed class ScriptSecretsTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _root;
    private readonly TestConfigStore _config;
    private readonly VaultService _vault;

    public ScriptSecretsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tb-ss-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _config = new TestConfigStore(Path.Combine(_root, "vault"));
        _vault = new VaultService(_config, () => new VaultOptions
        {
            AutoLockIdle = Timeout.InfiniteTimeSpan,
            Kdf          = new Argon2Parameters(MemoryKiB: 1024, Iterations: 1, Parallelism: 1),
        });
    }

    public void Dispose()
    {
        _vault.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task Script_CanResolveSecret_ViaGlobals()
    {
        await _vault.InitializeAsync("pw", Ct);
        await _vault.AddAsync("api", "token", "super-secret", ct: Ct);

        var globals = new ScriptGlobals(new ScriptSecrets(_vault, _ => Task.CompletedTask));

        var blaster = new ScriptBlaster();
        var output = new System.Collections.Generic.List<string>();

        var result = await blaster.RunAsync(
            "Console.WriteLine(Secrets.Resolve(\"api\", \"token\"));",
            scriptPath: null,
            output.Add,
            globals,
            CancellationToken.None);

        Assert.Equal(BlastStatus.Ok, result.Status);
        Assert.Contains("super-secret", output);
    }

    [Fact]
    public async Task Script_CanPassResolverDelegate_ToLibraryStyleApi()
    {
        // Emulates how AzureBlast / NetBlast will consume Secrets.Resolver:
        // the library takes a Func<category, key, ct, Task<string>> and
        // never sees the vault directly.
        await _vault.InitializeAsync("pw", Ct);
        await _vault.AddAsync("github", "pat", "ghp_xxx", ct: Ct);

        var globals = new ScriptGlobals(new ScriptSecrets(_vault, _ => Task.CompletedTask));

        var blaster = new ScriptBlaster();
        var output = new System.Collections.Generic.List<string>();

        const string script = """
            async Task<string> FakeClient(Func<string, string, System.Threading.CancellationToken, Task<string>> resolver)
                => await resolver("github", "pat", default);

            Console.WriteLine(await FakeClient(Secrets.Resolver));
            """;

        var result = await blaster.RunAsync(
            script, scriptPath: null, output.Add, globals, CancellationToken.None);

        Assert.Equal(BlastStatus.Ok, result.Status);
        Assert.Contains("ghp_xxx", output);
    }

    [Fact]
    public async Task Script_WhenVaultStaysLocked_AbortsAsCancelled_WithoutStackDump()
    {
        // User cancelled the unlock prompt (ensureUnlocked is a no-op and
        // vault stays locked). Script should:
        //   1. abort — Console.WriteLine("after") never runs,
        //   2. report as Cancelled (⊘), not Error (✗),
        //   3. surface only the clean message, no stack trace in output.
        var globals = new ScriptGlobals(new ScriptSecrets(_vault, _ => Task.CompletedTask));

        var blaster = new ScriptBlaster();
        var output = new System.Collections.Generic.List<string>();

        var result = await blaster.RunAsync(
            """
            Console.WriteLine("before");
            var v = Secrets.Resolve("api", "token");
            Console.WriteLine("after");
            """,
            scriptPath: null,
            output.Add,
            globals,
            CancellationToken.None);

        Assert.Equal(BlastStatus.Cancelled, result.Status);
        Assert.Equal("Vault is locked, cannot resolve secret.", result.Message);

        // Abort — "after" line was never reached.
        Assert.Contains("before", output);
        Assert.DoesNotContain("after", output);

        // No stack trace leaked to the terminal. Everything ScriptBlaster
        // writes goes through onOutput — the exception's type name, stack
        // frames, and "End of stack trace" marker must all be absent.
        Assert.DoesNotContain(output, line => line.Contains("at TaskBlaster.Engine."));
        Assert.DoesNotContain(output, line => line.Contains("VaultLockedException"));
        Assert.DoesNotContain(output, line => line.Contains("End of stack trace"));
    }

    [Fact]
    public async Task Keys_ReturnsKeysInCategory_WithoutValues()
    {
        await _vault.InitializeAsync("pw", Ct);
        await _vault.AddAsync("Azure", "test", "v1", ct: Ct);
        await _vault.AddAsync("Azure", "prod-sql", "v2", ct: Ct);
        await _vault.AddAsync("Github", "pat", "v3", ct: Ct);

        var secrets = new ScriptSecrets(_vault, _ => Task.CompletedTask);

        var keys = await secrets.KeysAsync("Azure", Ct);

        Assert.Equal(new[] { "prod-sql", "test" }, keys.OrderBy(k => k).ToArray());
        // Spot check: the values never made it into the list.
        Assert.DoesNotContain("v1", keys);
        Assert.DoesNotContain("v2", keys);
    }

    [Fact]
    public async Task Keys_IsCaseInsensitive_OnCategory()
    {
        await _vault.InitializeAsync("pw", Ct);
        await _vault.AddAsync("Azure", "test", "v1", ct: Ct);

        var secrets = new ScriptSecrets(_vault, _ => Task.CompletedTask);

        Assert.Single(await secrets.KeysAsync("azure", Ct));
        Assert.Single(await secrets.KeysAsync("AZURE", Ct));
    }

    [Fact]
    public async Task Categories_ReturnsPersistedCategoryCatalog()
    {
        await _vault.InitializeAsync("pw", Ct);
        await _vault.AddAsync("Azure", "test", "v", ct: Ct);
        await _vault.AddAsync("Github", "pat", "v", ct: Ct);

        var secrets = new ScriptSecrets(_vault, _ => Task.CompletedTask);

        var cats = await secrets.CategoriesAsync(Ct);

        Assert.Contains("Azure", cats);
        Assert.Contains("Github", cats);
    }

    [Fact]
    public async Task EnsureUnlocked_IsInvoked_BeforeResolve()
    {
        // Vault starts locked; the ensureUnlocked callback unlocks it
        // on-demand, the way MainWindow will when a script hits a locked
        // vault mid-run.
        await _vault.InitializeAsync("pw", Ct);
        await _vault.AddAsync("api", "token", "unlocked-value", ct: Ct);
        _vault.Lock();

        var unlockCalls = 0;
        var secrets = new ScriptSecrets(_vault, async ct =>
        {
            unlockCalls++;
            await _vault.UnlockAsync("pw", ct);
        });

        var value = await secrets.ResolveAsync("api", "token", Ct);

        Assert.Equal("unlocked-value", value);
        Assert.Equal(1, unlockCalls);
    }

    private sealed class TestConfigStore : IConfigStore
    {
        public TestConfigStore(string vaultFolder) => VaultFolder = vaultFolder;
        public string ScriptsFolder { get; set; } = "";
        public string FormsFolder   { get; set; } = "";
        public string VaultFolder   { get; set; }
        public string Theme         { get; set; } = "Industrial";
        public bool TerminalVisible { get; set; } = true;
        public string EditorHighlighter { get; set; } = "Native";
        public void Load() { }
        public void Save() { }
    }
}
