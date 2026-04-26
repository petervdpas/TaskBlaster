using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SecretBlast;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;
using TaskBlaster.Secrets;

namespace TaskBlaster.Tests;

public sealed class FormJsonExpanderTests : IDisposable
{
    private readonly string _root;
    private readonly TestConfigStore _config;
    private readonly VaultService _vault;

    public FormJsonExpanderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tb-fx-" + Guid.NewGuid().ToString("N"));
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
    public async Task StaticForm_PassesThroughUnchanged()
    {
        const string json = """
            { "title": "T", "fields": [
                { "key": "role", "type": "select",
                  "options": [ {"value":"A","label":"A"}, {"value":"B","label":"B"} ] } ] }
            """;

        var expanded = await FormJsonExpander.ExpandAsync(json, _vault);

        // Byte-identical means the expander skipped the rewrite entirely.
        Assert.Equal(json, expanded);
    }

    [Fact]
    public async Task VaultHint_IsReplacedWithMaterialisedOptions()
    {
        await _vault.InitializeAsync("pw");
        await _vault.AddAsync("Azure", "test",     "v1");
        await _vault.AddAsync("Azure", "prod-sql", "v2");
        await _vault.AddAsync("Github", "pat",     "v3"); // must NOT appear

        const string json = """
            { "fields": [
                { "key": "which", "type": "select",
                  "optionsFrom": { "source": "vault", "category": "Azure" } } ] }
            """;

        var expanded = await FormJsonExpander.ExpandAsync(json, _vault);

        var root = JsonNode.Parse(expanded)!.AsObject();
        var field = root["fields"]!.AsArray()[0]!.AsObject();

        Assert.Null(field["optionsFrom"]); // hint stripped
        var options = field["options"]!.AsArray();
        Assert.Equal(2, options.Count);

        var values = options.Select(o => (string)o!["value"]!).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "prod-sql", "test" }, values);

        // No vault values leak into the materialised options.
        Assert.DoesNotContain("v1", expanded);
        Assert.DoesNotContain("v2", expanded);
        Assert.DoesNotContain("v3", expanded);
    }

    [Fact]
    public async Task VaultHint_WithUserPickedOptions_PassesThemThroughAndStripsHint()
    {
        // Designer saved both `optionsFrom` (tag) and `options` (user's subset).
        // Expander must keep the subset and strip the tag — it is NOT an
        // "auto-expand everything in the category" marker when options exist.
        await _vault.InitializeAsync("pw");
        await _vault.AddAsync("Azure", "test",     "v1");
        await _vault.AddAsync("Azure", "prod-sql", "v2");
        await _vault.AddAsync("Azure", "stage-db", "v3");

        const string json = """
            { "fields": [
                { "key": "which", "type": "select",
                  "optionsFrom": { "source": "vault", "category": "Azure" },
                  "options": [ { "value": "test", "label": "Test DB" } ] } ] }
            """;

        var expanded = await FormJsonExpander.ExpandAsync(json, _vault);
        var field = JsonNode.Parse(expanded)!.AsObject()["fields"]!.AsArray()[0]!.AsObject();

        Assert.Null(field["optionsFrom"]);
        var options = field["options"]!.AsArray();
        Assert.Single(options);
        Assert.Equal("test",    (string)options[0]!["value"]!);
        Assert.Equal("Test DB", (string)options[0]!["label"]!);
    }

    [Fact]
    public async Task VaultHint_CategoryMissing_ThrowsClearly()
    {
        await _vault.InitializeAsync("pw");

        const string json = """
            { "fields": [
                { "key": "which", "type": "select",
                  "optionsFrom": { "source": "vault" } } ] }
            """;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => FormJsonExpander.ExpandAsync(json, _vault));
        Assert.Contains("category", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownSource_Throws()
    {
        const string json = """
            { "fields": [
                { "key": "x", "type": "select",
                  "optionsFrom": { "source": "carrier-pigeon", "category": "Azure" } } ] }
            """;

        await Assert.ThrowsAsync<NotSupportedException>(
            () => FormJsonExpander.ExpandAsync(json, _vault));
    }

    [Fact]
    public async Task NonObjectRoot_PassesThrough()
    {
        // Garbage in, garbage out — don't pretend to parse.
        const string json = "[]";
        var expanded = await FormJsonExpander.ExpandAsync(json, _vault);
        Assert.Equal(json, expanded);
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
