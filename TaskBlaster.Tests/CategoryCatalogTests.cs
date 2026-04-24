using System;
using System.Linq;
using TaskBlaster.Secrets;

namespace TaskBlaster.Tests;

public sealed class CategoryCatalogTests
{
    [Fact]
    public void Normalize_TrimsDedupesAndSorts()
    {
        var result = CategoryCatalog.Normalize(new[] { "  github  ", "Azure", "github", "azure", "", "backup" });
        Assert.Equal(new[] { "Azure", "backup", "github" }, result.ToArray());
    }

    [Fact]
    public void Normalize_KeepsFirstSeenCasing()
    {
        var result = CategoryCatalog.Normalize(new[] { "Azure", "AZURE", "azure" });
        Assert.Single(result);
        Assert.Equal("Azure", result[0]);
    }

    [Fact]
    public void RoundTrip_PreservesCategories()
    {
        var original = CategoryCatalog.Create(new[] { "azure", "github" });
        var json = original.ToJson();
        var decoded = CategoryCatalog.FromJson(json);

        Assert.Equal(original.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(original.Categories.ToArray(), decoded.Categories.ToArray());
        Assert.Equal(original.UpdatedUtc, decoded.UpdatedUtc);
    }

    [Fact]
    public void FromJson_Empty_Throws()
    {
        Assert.Throws<InvalidCategoryCatalogException>(() => CategoryCatalog.FromJson(""));
    }

    [Fact]
    public void FromJson_Malformed_Throws()
    {
        Assert.Throws<InvalidCategoryCatalogException>(() => CategoryCatalog.FromJson("{not-json"));
    }

    [Fact]
    public void FromJson_UnknownSchemaVersion_Throws()
    {
        var now = DateTime.UtcNow.ToString("O");
        var json = $"{{\"SchemaVersion\":99,\"Categories\":[\"a\"],\"UpdatedUtc\":\"{now}\"}}";
        Assert.Throws<InvalidCategoryCatalogException>(() => CategoryCatalog.FromJson(json));
    }

    [Fact]
    public void ReservedId_Is32HexChars()
    {
        Assert.Equal(32, CategoryCatalog.ReservedId.Length);
        foreach (var c in CategoryCatalog.ReservedId)
            Assert.True((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }
}
