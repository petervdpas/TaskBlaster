using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaskBlaster.Connections;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Ai;

/// <summary>
/// Anthropic provider. Posts a 5-token test message to
/// <c>{baseUrl}/v1/messages</c>; the response body is discarded —
/// we only care that the round-trip works.
/// </summary>
public sealed class AnthropicProvider : IAiProvider
{
    /// <summary>Anthropic's API version header value.</summary>
    public const string AnthropicVersion = "2023-06-01";

    public string Kind => "anthropic";

    public string DisplayName => "Anthropic";

    public IReadOnlyList<AiModelInfo> KnownModels { get; } = new[]
    {
        new AiModelInfo(
            Id:          "claude-opus-4-7",
            DisplayName: "Claude Opus 4.7",
            Notes:       "Highest capability, slowest, most expensive. Use for hard reasoning."),
        new AiModelInfo(
            Id:          "claude-sonnet-4-6",
            DisplayName: "Claude Sonnet 4.6",
            Notes:       "Balanced; good default for most use."),
        new AiModelInfo(
            Id:          "claude-haiku-4-5-20251001",
            DisplayName: "Claude Haiku 4.5",
            Notes:       "Fastest, cheapest. Use for high-volume / simple operations."),
    };

    public async Task<AiPingResult> PingAsync(
        Connection connection,
        IVaultService vault,
        HttpClient http,
        CancellationToken ct)
    {
        try
        {
            var apiKey  = await AiClient.ResolveFieldAsync(connection, "apikey",  vault, ct).ConfigureAwait(false);
            var baseUrl = await AiClient.ResolveFieldAsync(connection, "baseUrl", vault, ct).ConfigureAwait(false);
            var model   = await AiClient.ResolveFieldAsync(connection, "model",   vault, ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(apiKey))  return AiPingResult.Fail("Connection has no apikey field (or it resolved empty).");
            if (string.IsNullOrEmpty(baseUrl)) return AiPingResult.Fail("Connection has no baseUrl field.");
            if (string.IsNullOrEmpty(model))   return AiPingResult.Fail("Connection has no model field.");

            // Tolerate baseUrl with or without /v1/messages — both shapes
            // exist in user setups and we don't want to make them re-edit.
            var url = baseUrl.TrimEnd('/');
            if (!url.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
                url += "/v1/messages";

            var body = JsonSerializer.Serialize(new
            {
                model,
                max_tokens = 5,
                messages = new[] { new { role = "user", content = "ping" } },
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", AnthropicVersion);

            var sw = Stopwatch.StartNew();
            using var response = await http.SendAsync(req, ct).ConfigureAwait(false);
            sw.Stop();

            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                return AiPingResult.Ok($"Connected. Model responded in {sw.ElapsedMilliseconds} ms.", sw.Elapsed, status);

            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return AiPingResult.Fail(MapErrorMessage(response.StatusCode, errorBody), sw.Elapsed, status);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            return AiPingResult.Fail("Test cancelled.");
        }
        catch (HttpRequestException ex)
        {
            return AiPingResult.Fail($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AiPingResult.Fail($"Unexpected error: {ex.Message}");
        }
    }

    private static string MapErrorMessage(HttpStatusCode status, string errorBody) => (int)status switch
    {
        401 => "Invalid API key.",
        403 => "API key forbidden — check workspace / scope on the Anthropic console.",
        404 => "Endpoint or model not found — check baseUrl and the model name.",
        429 => "Rate-limited. Try again in a moment.",
        500 or 502 or 503 or 504 => "Anthropic server error. Try again in a moment.",
        _   => $"HTTP {(int)status}: {Truncate(errorBody, 200)}",
    };

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
