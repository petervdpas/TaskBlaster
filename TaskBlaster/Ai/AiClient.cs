using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
/// One turn in a chat. Role is <c>"user"</c> or <c>"assistant"</c>;
/// the system prompt lives outside this collection on
/// <see cref="IAiProvider.SendAsync"/>.
/// </summary>
public sealed record AiMessage(string Role, string Content)
{
    /// <summary>Convenience: a user turn.</summary>
    public static AiMessage User(string content) => new("user", content);
    /// <summary>Convenience: an assistant turn.</summary>
    public static AiMessage Assistant(string content) => new("assistant", content);
}

/// <summary>
/// Outcome of one chat-completion call. <see cref="Text"/> is the model's
/// response when <see cref="Success"/> is true; <see cref="Error"/> carries
/// a user-facing failure message otherwise. <see cref="StopReason"/> is
/// the provider's hint about how the response ended — particularly
/// <c>"max_tokens"</c>, which means the model ran out of budget mid-thought
/// and the caller probably wants to raise the limit.
/// </summary>
public sealed record AiCompletionResult(
    bool Success,
    string? Text,
    string? Error,
    int? StatusCode,
    TimeSpan? Latency,
    string? StopReason = null)
{
    public static AiCompletionResult Ok(string text, TimeSpan latency, int? status = 200, string? stopReason = null)
        => new(true, text, null, status, latency, stopReason);
    public static AiCompletionResult Fail(string error, TimeSpan? latency = null, int? status = null)
        => new(false, null, error, status, latency, null);
}

/// <summary>
/// Connection-aware resolver delegate. Same shape Blast libraries already
/// consume (NetworkBlast, AzureBlast): the first arg is the connection
/// name (== resolver category in the Blast convention), the second is the
/// field name. Plaintext fields return their literal; vault-backed fields
/// go through whatever vault machinery the host wires in. A field that
/// doesn't exist or resolves empty MUST come back as <c>string.Empty</c>
/// — providers treat absent and empty the same way.
/// </summary>
public delegate Task<string> ConnectionFieldResolver(
    string connectionName,
    string fieldName,
    CancellationToken ct);

/// <summary>
/// One AI provider. Implementations are stateless and registered in
/// DI; <see cref="AiClient"/> dispatches to whichever one matches the
/// connection's <c>kind</c> field. The interface is deliberately
/// vault-free and connection-store-free: the provider never sees a
/// <c>Connection</c> object or an <c>IVaultService</c>, only a
/// <see cref="ConnectionFieldResolver"/>. That keeps providers easy to
/// extract into a future <c>AgentBlast</c> nuget without dragging
/// TaskBlaster types along.
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
    Task<AiPingResult> PingAsync(
        string connectionName,
        ConnectionFieldResolver resolver,
        HttpClient http,
        CancellationToken ct);

    /// <summary>
    /// Send a chat completion. <paramref name="systemPrompt"/> is the
    /// stable directing context; <paramref name="messages"/> is the
    /// turn-by-turn conversation history (oldest first), with the latest
    /// user message as the final entry.
    /// </summary>
    Task<AiCompletionResult> SendAsync(
        string connectionName,
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        ConnectionFieldResolver resolver,
        HttpClient http,
        CancellationToken ct);
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
    private readonly ConnectionFieldResolver _resolver;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providersByKind;

    public AiClient(
        HttpClient http,
        IEnumerable<IAiProvider> providers,
        ConnectionFieldResolver resolver)
    {
        _http = http;
        _resolver = resolver;
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
    /// Ping the configured provider. The connection (looked up by name
    /// through the wired resolver) must carry a plaintext <c>kind</c>
    /// field whose value matches a registered provider.
    /// </summary>
    public async Task<AiPingResult> PingAsync(
        string connectionName,
        CancellationToken ct = default)
    {
        var kind = await _resolver(connectionName, "kind", ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(kind))
            return AiPingResult.Fail(
                "Connection has no 'kind' field. Add a plaintext field 'kind' with one of: "
                + string.Join(", ", _providersByKind.Keys));

        if (!_providersByKind.TryGetValue(kind, out var provider))
            return AiPingResult.Fail(
                $"No provider registered for kind '{kind}'. Known kinds: "
                + string.Join(", ", _providersByKind.Keys));

        return await provider.PingAsync(connectionName, _resolver, _http, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a chat completion via the configured provider. Same dispatch
    /// rules as <see cref="PingAsync"/>: the connection's <c>kind</c>
    /// field selects the provider.
    /// </summary>
    public async Task<AiCompletionResult> SendAsync(
        string connectionName,
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        CancellationToken ct = default)
    {
        var kind = await _resolver(connectionName, "kind", ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(kind))
            return AiCompletionResult.Fail(
                "Connection has no 'kind' field. Add a plaintext field 'kind' with one of: "
                + string.Join(", ", _providersByKind.Keys));

        if (!_providersByKind.TryGetValue(kind, out var provider))
            return AiCompletionResult.Fail(
                $"No provider registered for kind '{kind}'. Known kinds: "
                + string.Join(", ", _providersByKind.Keys));

        return await provider.SendAsync(connectionName, systemPrompt, messages, _resolver, _http, ct).ConfigureAwait(false);
    }
}
