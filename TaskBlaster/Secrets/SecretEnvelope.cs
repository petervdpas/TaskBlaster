using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskBlaster.Secrets;

/// <summary>
/// JSON envelope stored as the *value* of each SecretBlast secret. SecretBlast
/// itself only sees an opaque id (GUID) and an opaque string blob; TaskBlaster
/// owns the schema inside the blob — category/key/value + created/updated
/// timestamps. Category renames etc. happen by rewriting envelopes, never by
/// renaming files.
/// </summary>
public sealed record SecretEnvelope(
    int SchemaVersion,
    string Category,
    string Key,
    string Value,
    string? Description,
    DateTime CreatedUtc,
    DateTime UpdatedUtc)
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static SecretEnvelope Create(string category, string key, string value, string? description = null, DateTime? nowUtc = null)
    {
        var ts = nowUtc ?? DateTime.UtcNow;
        return new SecretEnvelope(
            SchemaVersion: CurrentSchemaVersion,
            Category:      Normalize(category),
            Key:           Normalize(key),
            Value:         value ?? string.Empty,
            Description:   string.IsNullOrWhiteSpace(description) ? null : description,
            CreatedUtc:    ts,
            UpdatedUtc:    ts);
    }

    public SecretEnvelope With(string? category = null, string? key = null, string? value = null, string? description = null, DateTime? nowUtc = null)
    {
        return this with
        {
            Category    = category is null ? Category : Normalize(category),
            Key         = key      is null ? Key      : Normalize(key),
            Value       = value    ?? Value,
            Description = description is null
                ? Description
                : (string.IsNullOrWhiteSpace(description) ? null : description),
            UpdatedUtc  = nowUtc ?? DateTime.UtcNow,
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static SecretEnvelope FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidSecretEnvelopeException("Envelope JSON is empty.");

        SecretEnvelope? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SecretEnvelope>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidSecretEnvelopeException("Envelope JSON is malformed.", ex);
        }
        if (parsed is null)
            throw new InvalidSecretEnvelopeException("Envelope JSON deserialized to null.");

        if (parsed.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidSecretEnvelopeException(
                $"Unsupported envelope schema version {parsed.SchemaVersion}; expected {CurrentSchemaVersion}.");
        if (string.IsNullOrWhiteSpace(parsed.Category))
            throw new InvalidSecretEnvelopeException("Envelope category is empty.");
        if (string.IsNullOrWhiteSpace(parsed.Key))
            throw new InvalidSecretEnvelopeException("Envelope key is empty.");

        return parsed;
    }

    private static string Normalize(string s) => (s ?? string.Empty).Trim();
}

public sealed class InvalidSecretEnvelopeException : Exception
{
    public InvalidSecretEnvelopeException(string message) : base(message) { }
    public InvalidSecretEnvelopeException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Generates the opaque SecretBlast names under which envelopes are stored.
/// We use a 32-char hex GUID so filenames on disk are uniform and leak no
/// metadata about category or key.
/// </summary>
public static class SecretId
{
    public static string NewId() => Guid.NewGuid().ToString("N");
}
