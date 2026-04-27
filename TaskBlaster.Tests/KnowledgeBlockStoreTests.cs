using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskBlaster.Knowledge;

namespace TaskBlaster.Tests;

/// <summary>
/// Tests covering <see cref="KnowledgeBlockStore"/>: file scan, frontmatter
/// parse, body extraction, round-trip through Save, delete, and the
/// edge cases that come up when users hand-edit the markdown files.
/// </summary>
public sealed class KnowledgeBlockStoreTests : IDisposable
{
    private readonly string _temp;

    public KnowledgeBlockStoreTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "tb-knowledge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void List_EmptyFolder_ReturnsEmpty()
    {
        var store = new KnowledgeBlockStore(_temp);
        Assert.Empty(store.List());
    }

    [Fact]
    public void List_NonexistentFolder_ReturnsEmpty()
    {
        var nonexistent = Path.Combine(_temp, "nope");
        var store = new KnowledgeBlockStore(nonexistent);
        Assert.Empty(store.List());
    }

    [Fact]
    public void Reload_ReadsMarkdownFiles_AndParsesFrontmatter()
    {
        File.WriteAllText(Path.Combine(_temp, "mssql-rules.md"),
            "---\ntitle: Mssql do's and don'ts\nwhen: AzureBlast.MssqlDatabase\npriority: 5\n---\n\nAlways dispose.\n");

        var store = new KnowledgeBlockStore(_temp);
        var blocks = store.List();

        Assert.Single(blocks);
        var b = blocks[0];
        Assert.Equal("mssql-rules", b.Id);
        Assert.Equal("Mssql do's and don'ts", b.Title);
        Assert.Equal("AzureBlast.MssqlDatabase", b.Frontmatter["when"]);
        Assert.Equal("5", b.Frontmatter["priority"]);
        Assert.Equal("Always dispose.\n", b.Body);
    }

    [Fact]
    public void Reload_FileWithoutFrontmatter_HasHumanisedTitle_AndFullBody()
    {
        File.WriteAllText(Path.Combine(_temp, "secret-handling.md"),
            "Use the vault. Never log secrets.\n");

        var store = new KnowledgeBlockStore(_temp);
        var b = store.Get("secret-handling")!;

        Assert.Equal("Secret Handling", b.Title);
        Assert.Empty(b.Frontmatter);
        Assert.Equal("Use the vault. Never log secrets.\n", b.Body);
    }

    [Fact]
    public void Save_WritesFrontmatterAndBody_BackToDisk()
    {
        var store = new KnowledgeBlockStore(_temp);
        var block = new KnowledgeBlock(
            "logging",
            "Logging conventions",
            "Always log at the boundary, never inside loops.\n",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["when"] = "any script",
                ["priority"] = "3",
            });

        store.Save(block);

        var raw = File.ReadAllText(Path.Combine(_temp, "logging.md"));
        Assert.StartsWith("---\n", raw);
        Assert.Contains("title: Logging conventions", raw);
        Assert.Contains("when: any script", raw);
        Assert.Contains("priority: 3", raw);
        Assert.EndsWith("Always log at the boundary, never inside loops.\n", raw);
    }

    [Fact]
    public void Save_ThenReload_RoundTripsBlock()
    {
        var first = new KnowledgeBlockStore(_temp);
        first.Save(new KnowledgeBlock(
            "round-trip",
            "Round trip",
            "Body line 1\n\nBody line 2 with `code`.\n",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["when"] = "always",
            }));

        var second = new KnowledgeBlockStore(_temp);
        var b = second.Get("round-trip")!;

        Assert.Equal("Round trip", b.Title);
        Assert.Equal("always", b.Frontmatter["when"]);
        Assert.Equal("Body line 1\n\nBody line 2 with `code`.\n", b.Body);
    }

    [Fact]
    public void Save_OmitsTitleFrontmatter_WhenTitleEqualsHumanisedId()
    {
        // If the user never customises the title, we don't pollute the file
        // with an auto-generated title line — keeps hand-authored files clean.
        var store = new KnowledgeBlockStore(_temp);
        store.Save(new KnowledgeBlock(
            "plain-block",
            "Plain Block",
            "Just a body.\n",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));

        var raw = File.ReadAllText(Path.Combine(_temp, "plain-block.md"));
        Assert.DoesNotContain("title:", raw);
        Assert.Equal("Just a body.\n", raw);
    }

    [Fact]
    public void Delete_RemovesFile_AndEntryFromList()
    {
        File.WriteAllText(Path.Combine(_temp, "doomed.md"), "content");
        var store = new KnowledgeBlockStore(_temp);
        Assert.NotNull(store.Get("doomed"));

        store.Delete("doomed");

        Assert.Null(store.Get("doomed"));
        Assert.False(File.Exists(Path.Combine(_temp, "doomed.md")));
    }

    [Fact]
    public void Delete_MissingId_DoesNotThrow()
    {
        var store = new KnowledgeBlockStore(_temp);
        store.Delete("never-existed"); // no throw
    }

    [Fact]
    public void Reload_UnterminatedFrontmatter_TreatsWholeFileAsBody()
    {
        // Hand-edited file with a stray "---" at the top but no closing
        // fence shouldn't lose the user's content — fall back to body-only.
        File.WriteAllText(Path.Combine(_temp, "broken.md"),
            "---\ntitle: never closed\nbody starts here\n");

        var store = new KnowledgeBlockStore(_temp);
        var b = store.Get("broken")!;

        Assert.Equal("Broken", b.Title);  // humanised fallback
        Assert.Empty(b.Frontmatter);
        Assert.Equal("---\ntitle: never closed\nbody starts here\n", b.Body);
    }

    [Fact]
    public void Reload_HandlesCrlfLineEndings()
    {
        // Windows-saved files end lines with \r\n; the parser must tolerate them.
        File.WriteAllText(Path.Combine(_temp, "crlf.md"),
            "---\r\ntitle: Windows file\r\n---\r\n\r\nBody.\r\n");

        var store = new KnowledgeBlockStore(_temp);
        var b = store.Get("crlf")!;

        Assert.Equal("Windows file", b.Title);
        Assert.Equal("Body.\r\n", b.Body);
    }

    [Fact]
    public void Parse_MalformedFrontmatterLine_IsSkipped_RestSurvives()
    {
        File.WriteAllText(Path.Combine(_temp, "mixed.md"),
            "---\ntitle: Good\nbroken-line-without-colon\nwhen: always\n---\nbody\n");

        var store = new KnowledgeBlockStore(_temp);
        var b = store.Get("mixed")!;

        Assert.Equal("Good", b.Title);
        Assert.Equal("always", b.Frontmatter["when"]);
        Assert.False(b.Frontmatter.ContainsKey("broken-line-without-colon"));
    }

    [Fact]
    public void Save_NormalisesFrontmatterKeyOrdering_TitleWhenPriorityFirst()
    {
        var store = new KnowledgeBlockStore(_temp);
        store.Save(new KnowledgeBlock(
            "ordered",
            "Ordered Block",
            "x\n",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["zeta"] = "z",
                ["priority"] = "1",
                ["alpha"] = "a",
                ["when"] = "always",
            }));

        var raw = File.ReadAllText(Path.Combine(_temp, "ordered.md"));
        var fenceBody = raw.Split("---", StringSplitOptions.None);
        // Frontmatter is between the first and second fence (index 1).
        var fmLines = fenceBody[1]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split(':', 2)[0].Trim())
            .ToList();

        Assert.Equal(new[] { "title", "when", "priority", "alpha", "zeta" }, fmLines);
    }

    [Fact]
    public void List_OrdersByTitle_CaseInsensitive()
    {
        File.WriteAllText(Path.Combine(_temp, "b.md"), "---\ntitle: banana\n---\n");
        File.WriteAllText(Path.Combine(_temp, "a.md"), "---\ntitle: Apple\n---\n");
        File.WriteAllText(Path.Combine(_temp, "c.md"), "---\ntitle: cherry\n---\n");

        var store = new KnowledgeBlockStore(_temp);
        var titles = store.List().Select(b => b.Title).ToArray();

        Assert.Equal(new[] { "Apple", "banana", "cherry" }, titles);
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        var store = new KnowledgeBlockStore(_temp);
        store.Save(new KnowledgeBlock("overw", "First", "v1\n",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        store.Save(new KnowledgeBlock("overw", "Second", "v2\n",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));

        var b = store.Get("overw")!;
        Assert.Equal("Second", b.Title);
        Assert.Equal("v2\n", b.Body);
    }

    [Fact]
    public void Save_CreatesFolderIfMissing()
    {
        // Settings change can move the knowledge folder underneath us;
        // first Save after a fresh folder shouldn't fail because of it.
        var subfolder = Path.Combine(_temp, "fresh");
        var store = new KnowledgeBlockStore(subfolder);
        store.Save(new KnowledgeBlock("seed", "Seed", "hi\n",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));

        Assert.True(File.Exists(Path.Combine(subfolder, "seed.md")));
    }
}
