using System;
using System.IO;
using TaskBlaster.Forms;

namespace TaskBlaster.Tests;

public sealed class FormDocumentIoTests : IDisposable
{
    private readonly string _temp;

    public FormDocumentIoTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "tb-doc-io-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
    }

    [Fact]
    public void LoadFromFile_ValidJson_BuildsDocument()
    {
        var path = Path.Combine(_temp, "form.json");
        File.WriteAllText(path, """
            { "title": "Test", "fields": [ { "key": "a", "type": "text" } ] }
            """);

        var doc = FormDocument.LoadFromFile(path);

        Assert.Equal("Test", doc.Title);
        Assert.Single(doc.Fields);
        Assert.Equal("a", doc.Fields[0].Key);
    }

    [Fact]
    public void LoadFromFile_MalformedJson_FallsBackToDefault()
    {
        var path = Path.Combine(_temp, "bad.json");
        File.WriteAllText(path, "this is definitely not json {{{");

        var doc = FormDocument.LoadFromFile(path);

        Assert.NotNull(doc); // didn't throw
        Assert.Empty(doc.Fields);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void LoadFromFile_MissingFile_ThrowsIoException()
    {
        var path = Path.Combine(_temp, "does-not-exist.json");

        Assert.Throws<FileNotFoundException>(() => FormDocument.LoadFromFile(path));
    }

    [Fact]
    public void LoadFromFile_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => FormDocument.LoadFromFile(""));
        Assert.Throws<ArgumentException>(() => FormDocument.LoadFromFile("   "));
    }

    [Fact]
    public void SaveToFile_RoundTrips_AndClearsDirty()
    {
        var doc = new FormDocument();
        doc.Title = "Roundtrip";
        var field = doc.AddField();
        Assert.True(doc.IsDirty);

        var path = Path.Combine(_temp, "out.json");
        doc.SaveToFile(path);

        Assert.False(doc.IsDirty);
        var reloaded = FormDocument.LoadFromFile(path);
        Assert.Equal("Roundtrip", reloaded.Title);
        Assert.Single(reloaded.Fields);
        Assert.Equal(field.Key, reloaded.Fields[0].Key);
    }

    [Fact]
    public void SaveToFile_CreatesNestedDirectories()
    {
        var nested = Path.Combine(_temp, "does", "not", "exist", "form.json");

        var doc = new FormDocument();
        doc.SaveToFile(nested);

        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void SaveToFile_EmptyPath_ThrowsArgumentException()
    {
        var doc = new FormDocument();
        Assert.Throws<ArgumentException>(() => doc.SaveToFile(""));
    }
}
