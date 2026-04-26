using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskBlaster.Connections;

namespace TaskBlaster.Tests;

/// <summary>
/// Round-trip + parse-edge-case tests for <see cref="ConnectionStore"/>.
/// Exercises the on-disk JSON shape directly so a hand-edited or migrated
/// file produces the expected in-memory connections.
/// </summary>
public sealed class ConnectionStoreTests : IDisposable
{
    private readonly string _file;

    public ConnectionStoreTests()
    {
        _file = Path.Combine(Path.GetTempPath(), $"tb-conn-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { File.Delete(_file); } catch { }
    }

    [Fact]
    public void EmptyFile_LoadsAsEmpty()
    {
        var store = new ConnectionStore(_file);
        Assert.Empty(store.List());
    }

    [Fact]
    public void SaveThenList_RoundTripsPlaintextAndVaultRefs()
    {
        var store = new ConnectionStore(_file);
        var conn = new Connection("github", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("https://api.github.com"),
            ["token"]   = ConnectionField.OfVault("github-secrets", "pat"),
        });
        store.Save(conn);

        var fresh = new ConnectionStore(_file);
        var loaded = fresh.Get("github");
        Assert.NotNull(loaded);
        Assert.Equal("https://api.github.com", loaded!.Fields["baseUrl"].Value);
        Assert.Null(loaded.Fields["baseUrl"].FromVault);
        Assert.Equal("github-secrets", loaded.Fields["token"].FromVault!.Category);
        Assert.Equal("pat",             loaded.Fields["token"].FromVault!.Key);
        Assert.Null(loaded.Fields["token"].Value);
    }

    [Fact]
    public void Get_IsCaseInsensitive_ButFieldKeysArePreserved()
    {
        var store = new ConnectionStore(_file);
        store.Save(new Connection("GitHub", new Dictionary<string, ConnectionField>
        {
            ["baseUrl"] = ConnectionField.Plaintext("u"),
        }));

        Assert.NotNull(store.Get("github"));
        Assert.NotNull(store.Get("GITHUB"));
        Assert.True(store.Get("github")!.Fields.ContainsKey("baseUrl"));
        Assert.False(store.Get("github")!.Fields.ContainsKey("BASEURL")); // field keys are exact-match
    }

    [Fact]
    public void Remove_DropsConnection_AndPersists()
    {
        var store = new ConnectionStore(_file);
        store.Save(new Connection("a", new Dictionary<string, ConnectionField>
        {
            ["x"] = ConnectionField.Plaintext("1"),
        }));
        store.Save(new Connection("b", new Dictionary<string, ConnectionField>
        {
            ["x"] = ConnectionField.Plaintext("2"),
        }));

        store.Remove("a");
        var fresh = new ConnectionStore(_file);
        Assert.Null(fresh.Get("a"));
        Assert.NotNull(fresh.Get("b"));
    }

    [Fact]
    public void List_IsNameSortedCaseInsensitive()
    {
        var store = new ConnectionStore(_file);
        store.Save(new Connection("zebra",  new Dictionary<string, ConnectionField> { ["x"] = ConnectionField.Plaintext("1") }));
        store.Save(new Connection("Apple",  new Dictionary<string, ConnectionField> { ["x"] = ConnectionField.Plaintext("2") }));
        store.Save(new Connection("middle", new Dictionary<string, ConnectionField> { ["x"] = ConnectionField.Plaintext("3") }));

        var names = store.List().Select(c => c.Name).ToArray();
        Assert.Equal(new[] { "Apple", "middle", "zebra" }, names);
    }

    [Fact]
    public void Load_DropsMalformedFields_KeepsRest()
    {
        // Hand-write JSON with one bad field (neither value nor fromVault).
        File.WriteAllText(_file, """
        {
          "github": {
            "baseUrl": { "value": "https://api.github.com" },
            "broken":  { },
            "token":   { "fromVault": { "category": "github-secrets", "key": "pat" } }
          }
        }
        """);

        var store = new ConnectionStore(_file);
        var conn = store.Get("github")!;
        Assert.True(conn.Fields.ContainsKey("baseUrl"));
        Assert.True(conn.Fields.ContainsKey("token"));
        Assert.False(conn.Fields.ContainsKey("broken"));
    }

    [Fact]
    public void Load_MalformedJson_LeavesStoreEmpty()
    {
        File.WriteAllText(_file, "{ this is not json");
        var store = new ConnectionStore(_file);
        Assert.Empty(store.List());
    }
}
