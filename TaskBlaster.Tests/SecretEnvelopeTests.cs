using System;
using TaskBlaster.Secrets;

namespace TaskBlaster.Tests;

public sealed class SecretEnvelopeTests
{
    [Fact]
    public void Create_SetsSchemaVersion_AndEqualTimestamps()
    {
        var env = SecretEnvelope.Create("azure", "prod-sql", "Server=...;");
        Assert.Equal(SecretEnvelope.CurrentSchemaVersion, env.SchemaVersion);
        Assert.Equal(env.CreatedUtc, env.UpdatedUtc);
        Assert.Equal("azure", env.Category);
        Assert.Equal("prod-sql", env.Key);
        Assert.Null(env.Description);
    }

    [Fact]
    public void Create_TrimsCategoryAndKey()
    {
        var env = SecretEnvelope.Create("  azure  ", "\tprod-sql\n", "x");
        Assert.Equal("azure", env.Category);
        Assert.Equal("prod-sql", env.Key);
    }

    [Fact]
    public void Create_EmptyDescription_BecomesNull()
    {
        var env = SecretEnvelope.Create("c", "k", "v", "   ");
        Assert.Null(env.Description);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = SecretEnvelope.Create("azure", "prod-sql", "Server=foo;", "note");
        var json = original.ToJson();
        var decoded = SecretEnvelope.FromJson(json);

        Assert.Equal(original.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(original.Category,      decoded.Category);
        Assert.Equal(original.Key,           decoded.Key);
        Assert.Equal(original.Value,         decoded.Value);
        Assert.Equal(original.Description,   decoded.Description);
        Assert.Equal(original.CreatedUtc,    decoded.CreatedUtc);
        Assert.Equal(original.UpdatedUtc,    decoded.UpdatedUtc);
    }

    [Fact]
    public void With_BumpsUpdatedUtc_ButKeepsCreatedUtc()
    {
        var earlier = DateTime.UtcNow.AddMinutes(-10);
        var original = SecretEnvelope.Create("c", "k", "v", null, nowUtc: earlier);
        var later = earlier.AddMinutes(5);

        var updated = original.With(value: "v2", nowUtc: later);

        Assert.Equal(original.CreatedUtc, updated.CreatedUtc);
        Assert.Equal(later, updated.UpdatedUtc);
        Assert.Equal("v2", updated.Value);
    }

    [Fact]
    public void With_RenameCategory_Normalizes()
    {
        var env = SecretEnvelope.Create("old", "k", "v");
        var moved = env.With(category: "  new  ");
        Assert.Equal("new", moved.Category);
    }

    [Fact]
    public void FromJson_Empty_Throws()
    {
        Assert.Throws<InvalidSecretEnvelopeException>(() => SecretEnvelope.FromJson(""));
    }

    [Fact]
    public void FromJson_Malformed_Throws()
    {
        Assert.Throws<InvalidSecretEnvelopeException>(() => SecretEnvelope.FromJson("{not-json"));
    }

    [Fact]
    public void FromJson_MissingCategory_Throws()
    {
        var now = DateTime.UtcNow.ToString("O");
        var json = $"{{\"SchemaVersion\":1,\"Category\":\"\",\"Key\":\"k\",\"Value\":\"v\",\"Description\":null,\"CreatedUtc\":\"{now}\",\"UpdatedUtc\":\"{now}\"}}";
        Assert.Throws<InvalidSecretEnvelopeException>(() => SecretEnvelope.FromJson(json));
    }

    [Fact]
    public void FromJson_UnknownSchemaVersion_Throws()
    {
        var now = DateTime.UtcNow.ToString("O");
        var json = $"{{\"SchemaVersion\":99,\"Category\":\"c\",\"Key\":\"k\",\"Value\":\"v\",\"Description\":null,\"CreatedUtc\":\"{now}\",\"UpdatedUtc\":\"{now}\"}}";
        Assert.Throws<InvalidSecretEnvelopeException>(() => SecretEnvelope.FromJson(json));
    }

    [Fact]
    public void SecretId_NewId_Is32HexChars()
    {
        var id = SecretId.NewId();
        Assert.Equal(32, id.Length);
        foreach (var c in id)
            Assert.True((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }

    [Fact]
    public void SecretId_NewId_Unique()
    {
        var set = new System.Collections.Generic.HashSet<string>();
        for (var i = 0; i < 1000; i++) Assert.True(set.Add(SecretId.NewId()));
    }
}
