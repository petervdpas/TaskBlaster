using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Connections;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Ai;

/// <summary>
/// Outcome of a one-shot ping against a configured AI provider.
/// Designed to be rendered inline in the Settings AI tab — short
/// user-facing message in <see cref="Message"/>, structured fields
/// for tooling.
/// </summary>
public sealed record AiPingResult(
    bool Success,
    string Message,
    TimeSpan? Latency,
    int? StatusCode)
{
    public static AiPingResult Ok(string message, TimeSpan latency, int? statusCode = 200)
        => new(true, message, latency, statusCode);

    public static AiPingResult Fail(string message, TimeSpan? latency = null, int? statusCode = null)
        => new(false, message, latency, statusCode);
}

/// <summary>
/// One known model offered by an <see cref="IAiProvider"/>. The id is
/// the wire string the provider's API accepts; the display name is
/// for UI dropdowns. Notes carry a one-line capability hint
/// ("highest capability, slowest" / "balanced default" / etc.).
/// </summary>
public sealed record AiModelInfo(string Id, string DisplayName, string? Notes = null);

/// <summary>
/// One AI provider. Implementations are stateless and registered in
/// DI; <see cref="AiClient"/> dispatches to whichever one matches the
/// connection's <c>kind</c> field. Future verbs (ChatAsync,
/// StreamAsync, etc.) live here too — Ping is just the first.
/// </summary>
public interface IAiProvider
{
    /// <summary>The connection-<c>kind</c> value this provider answers to (e.g. "anthropic").</summary>
    string Kind { get; }

    /// <summary>Display name for UI ("Anthropic" / "OpenAI" / "Ollama").</summary>
    string DisplayName { get; }

    /// <summary>
    /// Models this provider knows about, ordered by capability (highest
    /// first by convention). Used by future model-picker UIs and as a
    /// hint for the ping flow when the connection's model field is
    /// blank or invalid.
    /// </summary>
    IReadOnlyList<AiModelInfo> KnownModels { get; }

    /// <summary>Send a minimal request and classify the response into success / friendly failure.</summary>
    Task<AiPingResult> PingAsync(Connection connection, IVaultService vault, HttpClient http, CancellationToken ct);
}

/// <summary>
/// Consumer-facing entry point for AI operations. Holds the registered
/// <see cref="IAiProvider"/>s and dispatches to the right one based on
/// the connection's <c>kind</c> field. Adding a new provider = adding
/// a new <see cref="IAiProvider"/> implementation + DI registration;
/// this class stays untouched.
/// </summary>
public sealed class AiClient
{
    private readonly HttpClient _http;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providersByKind;

    public AiClient(HttpClient http, IEnumerable<IAiProvider> providers)
    {
        _http = http;
        _providersByKind = providers.ToDictionary(
            p => p.Kind,
            p => p,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Provider kinds the client can dispatch to (for diagnostics / dropdowns).</summary>
    public IReadOnlyCollection<string> RegisteredKinds => _providersByKind.Keys.ToArray();

    /// <summary>Find a registered provider by kind, or null if none matches.</summary>
    public IAiProvider? FindProvider(string kind)
        => _providersByKind.TryGetValue(kind, out var p) ? p : null;

    /// <summary>Every registered provider (for "configure provider" UIs).</summary>
    public IReadOnlyList<IAiProvider> AllProviders => _providersByKind.Values.ToArray();

    /// <summary>
    /// Ping the configured provider. The connection must carry a plaintext
    /// <c>kind</c> field whose value matches a registered provider.
    /// </summary>
    public async Task<AiPingResult> PingAsync(
        Connection connection,
        IVaultService vault,
        CancellationToken ct = default)
    {
        var kind = await ResolveFieldAsync(connection, "kind", vault, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(kind))
            return AiPingResult.Fail(
                "Connection has no 'kind' field. Add a plaintext field 'kind' with one of: "
                + string.Join(", ", _providersByKind.Keys));

        if (!_providersByKind.TryGetValue(kind, out var provider))
            return AiPingResult.Fail(
                $"No provider registered for kind '{kind}'. Known kinds: "
                + string.Join(", ", _providersByKind.Keys));

        return await provider.PingAsync(connection, vault, _http, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Look up a connection field case-insensitively and resolve it.
    /// Plaintext fields return the literal; vault-backed fields go
    /// through <paramref name="vault"/>. Missing or empty → empty string,
    /// so callers can treat absent and empty fields the same way.
    /// </summary>
    public static async Task<string> ResolveFieldAsync(
        Connection conn,
        string fieldName,
        IVaultService vault,
        CancellationToken ct = default)
    {
        var match = conn.Fields.FirstOrDefault(kvp =>
            string.Equals(kvp.Key, fieldName, StringComparison.OrdinalIgnoreCase));
        if (match.Value is null) return string.Empty;

        var field = match.Value;
        if (!string.IsNullOrEmpty(field.Value)) return field.Value;
        if (field.FromVault is { } vr) return await vault.ResolveAsync(vr.Category, vr.Key, ct).ConfigureAwait(false);
        return string.Empty;
    }
}
