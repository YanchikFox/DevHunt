using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevHunt.CoreApi.Services.Ai.Llm.Models;

namespace DevHunt.CoreApi.Services.Ai.Llm.Providers;

/// <summary>
/// Static config for an OpenAI-compatible endpoint. Most modern providers
/// (Groq, DeepSeek, OpenRouter, xAI, Mistral, Together, Fireworks, ...) speak
/// the OpenAI <c>/chat/completions</c> protocol, so a single adapter parametrised
/// with <see cref="BaseUrl"/> covers them all.
/// </summary>
/// <param name="ProviderId">Stable lowercase provider id used in DB and DI lookup.</param>
/// <param name="DisplayName">Human-readable provider name for UI.</param>
/// <param name="BaseUrl">Provider base API URL ending with the OpenAI-compatible version path.</param>
/// <param name="DefaultHeaders">Extra default headers, e.g. OpenRouter attribution headers.</param>
/// <param name="SkipModelListingForValidation">Skip <c>GET /models</c> validation for providers that do not expose it.</param>
public sealed record OpenAiCompatibleConfig(
    string ProviderId,
    string DisplayName,
    Uri BaseUrl,
    IReadOnlyDictionary<string, string>? DefaultHeaders = null,
    bool SkipModelListingForValidation = false
);

/// <summary>
/// Single adapter that fits every OpenAI-compatible service. Streaming, tools,
/// stop sequences, system prompts - all share one wire format. Concrete
/// providers are registered as DI Singletons via
/// <see cref="LlmProviderExtensions.AddOpenAiCompatibleProvider"/>.
/// </summary>
public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly OpenAiCompatibleConfig _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OpenAiCompatibleProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiCompatibleProvider"/> class.
    /// </summary>
    /// <param name="config">OpenAI-compatible provider configuration.</param>
    /// <param name="httpFactory">Factory for named provider HTTP clients.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public OpenAiCompatibleProvider(
        OpenAiCompatibleConfig config,
        IHttpClientFactory httpFactory,
        ILogger<OpenAiCompatibleProvider> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderId => _config.ProviderId;
    /// <inheritdoc />
    public string DisplayName => _config.DisplayName;

    /// <inheritdoc />
    public async Task<KeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken ct)
    {
        if (_config.SkipModelListingForValidation)
        {
            // For providers that don't expose /models we'd need a tiny no-op
            // completion, but skipping listing is rare; treat as "trust on
            // first use" for now. Not currently used by any registered provider.
            return new KeyValidationResult(true, null, "Validation skipped (no /models endpoint).");
        }

        using var client = CreateHttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "models");
        ApplyAuth(req, apiKey);

        try
        {
            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                return new KeyValidationResult(true);
            }

            // Distinguish "your key is bad" from "we're down" so the user gets
            // a helpful message either way.
            var status = (int)resp.StatusCode;
            var bodyPreview = await ReadBodyPreviewAsync(resp, ct);
            return status is 401 or 403
                ? new KeyValidationResult(false, $"Provider rejected the key (HTTP {status}). {bodyPreview}".Trim())
                : new KeyValidationResult(false, $"Provider returned HTTP {status}. {bodyPreview}".Trim());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "{Provider} validation transport error", _config.ProviderId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderModelInfo>> ListModelsAsync(string apiKey, CancellationToken ct)
    {
        using var client = CreateHttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "models");
        ApplyAuth(req, apiKey);

        using var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<OaiModelsResponse>(JsonOpts, ct);
        if (payload?.Data == null) return Array.Empty<ProviderModelInfo>();

        return payload.Data
            .Where(m => !string.IsNullOrWhiteSpace(m.Id))
            .Select(m => new ProviderModelInfo(
                m.Id!,
                m.Name ?? m.Id,
                m.ContextLength,
                SupportsTools: m.SupportedParameters?.Contains("tools", StringComparer.OrdinalIgnoreCase),
                SupportsStreaming: m.SupportedParameters?.Contains("stream", StringComparer.OrdinalIgnoreCase) ?? true,
                SupportsVision: m.Architecture?.InputModalities?.Contains("image", StringComparer.OrdinalIgnoreCase),
                InputPricePer1M: PricePerTokenToPerMillion(m.Pricing?.Prompt),
                OutputPricePer1M: PricePerTokenToPerMillion(m.Pricing?.Completion),
                MaxOutputTokens: m.TopProvider?.MaxCompletionTokens))
            .ToList();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmChatChunk> StreamChatAsync(
        LlmChatRequest request,
        string apiKey,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var client = CreateHttpClient();
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(BuildPayload(request), options: JsonOpts),
        };
        ApplyAuth(httpReq, apiKey);
        httpReq.Headers.Accept.ParseAdd("text/event-stream");

        // ResponseHeadersRead lets us start consuming the SSE stream as soon as
        // headers arrive - without it HttpClient buffers the whole body and
        // streaming becomes pointless.
        using var resp = await client.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var bodyPreview = await ReadBodyPreviewAsync(resp, ct);
            throw new LlmProviderException(
                _config.ProviderId,
                (int)resp.StatusCode,
                $"{_config.DisplayName} returned HTTP {(int)resp.StatusCode}. {bodyPreview}".Trim());
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        await foreach (var ev in SseEventReader.ReadAsync(stream, ct))
        {
            if (ev.Data == "[DONE]") yield break;
            if (string.IsNullOrWhiteSpace(ev.Data)) continue;

            OaiStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OaiStreamChunk>(ev.Data, JsonOpts);
            }
            catch (JsonException ex)
            {
                // Don't crash a long stream over one malformed frame - log and
                // skip. Real failures still surface via HTTP status above.
                _logger.LogDebug(ex, "{Provider}: ignored malformed SSE frame", _config.ProviderId);
                continue;
            }

            if (chunk == null) continue;

            var converted = ConvertChunk(chunk);
            if (converted != null) yield return converted;
        }
    }

    // --- internals --------------------------------------------------------

    /// <summary>Creates the named provider client with base URL, default headers, and streaming-safe timeout.</summary>
    private HttpClient CreateHttpClient()
    {
        var client = _httpFactory.CreateClient($"llm:{_config.ProviderId}");
        client.BaseAddress = _config.BaseUrl;
        // Streaming completions can run for minutes - disable HttpClient's
        // default 100s timeout and let CancellationToken control termination.
        client.Timeout = Timeout.InfiniteTimeSpan;
        if (_config.DefaultHeaders != null)
        {
            foreach (var (k, v) in _config.DefaultHeaders)
            {
                if (!client.DefaultRequestHeaders.Contains(k))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(k, v);
                }
            }
        }
        return client;
    }

    /// <summary>Applies bearer-token authentication expected by OpenAI-compatible providers.</summary>
    private static void ApplyAuth(HttpRequestMessage req, string apiKey)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>Reads a bounded response-body preview for validation and error messages.</summary>
    private static async Task<string> ReadBodyPreviewAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            return body.Length > 300 ? body[..300] + "..." : body;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Builds the OpenAI-compatible chat-completions payload with streaming and optional tools.</summary>
    private object BuildPayload(LlmChatRequest request)
    {
        return new
        {
            model = request.ModelId,
            messages = request.Messages.Select(BuildMessage).ToArray(),
            tools = request.Tools is { Count: > 0 }
                ? request.Tools.Select(BuildTool).ToArray()
                : null,
            temperature = request.Temperature,
            top_p = request.TopP,
            max_tokens = request.MaxOutputTokens,
            stop = request.StopSequences,
            stream = true,
            stream_options = new { include_usage = true },
        };
    }

    /// <summary>Maps a canonical message to OpenAI-compatible chat message JSON.</summary>
    private static object BuildMessage(LlmMessage m) => m.Role switch
    {
        LlmRole.Tool => new
        {
            role = "tool",
            content = m.Content ?? string.Empty,
            tool_call_id = m.ToolCallId,
        },
        LlmRole.Assistant when m.ToolCalls is { Count: > 0 } => new
        {
            role = "assistant",
            content = m.Content,
            tool_calls = m.ToolCalls.Select(tc => new
            {
                id = tc.Id,
                type = "function",
                function = new { name = tc.Name, arguments = tc.ArgumentsJson },
            }).ToArray(),
        },
        _ => new
        {
            role = m.Role.ToString().ToLowerInvariant(),
            content = m.Content ?? string.Empty,
            name = m.Name,
        },
    };

    /// <summary>Wraps a canonical tool definition in the OpenAI function-tool envelope.</summary>
    private static object BuildTool(LlmToolDefinition t) => new
    {
        type = "function",
        function = new
        {
            name = t.Name,
            description = t.Description,
            parameters = t.ParametersSchema,
        },
    };

    /// <summary>Converts one OpenAI-compatible stream frame into a canonical chat chunk.</summary>
    private static LlmChatChunk? ConvertChunk(OaiStreamChunk chunk)
    {
        // Usage-only frames (final frame from providers that include usage)
        // come without choices - surface usage directly.
        var choice = chunk.Choices?.FirstOrDefault();
        var usage = chunk.Usage == null
            ? null
            : new LlmUsage(chunk.Usage.PromptTokens, chunk.Usage.CompletionTokens, chunk.Usage.TotalTokens);

        if (choice == null)
        {
            return usage == null ? null : new LlmChatChunk(Usage: usage);
        }

        var deltaText = choice.Delta?.Content;
        var toolDeltas = choice.Delta?.ToolCalls?
            .Select(tc => new LlmToolCallDelta(
                tc.Index,
                tc.Id,
                tc.Function?.Name,
                tc.Function?.Arguments))
            .ToList();

        var finish = MapFinishReason(choice.FinishReason);

        return new LlmChatChunk(
            DeltaText: deltaText,
            ToolCallDelta: toolDeltas,
            FinishReason: finish,
            Usage: usage);
    }

    /// <summary>Normalizes OpenAI finish reasons to <see cref="LlmFinishReason"/>.</summary>
    private static LlmFinishReason MapFinishReason(string? reason) => reason switch
    {
        null => LlmFinishReason.InProgress,
        "stop" => LlmFinishReason.Stop,
        "length" => LlmFinishReason.Length,
        "tool_calls" or "function_call" => LlmFinishReason.ToolCalls,
        "content_filter" => LlmFinishReason.ContentFilter,
        _ => LlmFinishReason.Other,
    };

    /// <summary>Converts OpenRouter per-token pricing strings to per-million token prices.</summary>
    private static decimal? PricePerTokenToPerMillion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var perToken)
            ? perToken * 1_000_000m
            : null;
    }

    // Wire DTOs - kept local to the adapter so the canonical model stays clean.
    /// <summary>
    /// Models the OpenAI-compatible JSON payload returned during model listing.
    /// </summary>
    private sealed record OaiModelsResponse(List<OaiModel>? Data);

    /// <summary>
    /// Models an OpenAI-compatible JSON model entry used during model listing.
    /// </summary>
    private sealed record OaiModel(
        string? Id,
        string? Name,
        [property: JsonPropertyName("context_length")] int? ContextLength,
        OaiModelPricing? Pricing,
        OaiModelArchitecture? Architecture,
        [property: JsonPropertyName("supported_parameters")] List<string>? SupportedParameters,
        [property: JsonPropertyName("top_provider")] OaiTopProvider? TopProvider);

    /// <summary>
    /// Models OpenAI-compatible pricing JSON used during model listing.
    /// </summary>
    private sealed record OaiModelPricing(string? Prompt, string? Completion);

    /// <summary>
    /// Models OpenAI-compatible architecture metadata used during model listing.
    /// </summary>
    private sealed record OaiModelArchitecture(
        [property: JsonPropertyName("input_modalities")] List<string>? InputModalities);

    /// <summary>
    /// Models OpenAI-compatible top-provider limits used during model listing.
    /// </summary>
    private sealed record OaiTopProvider(
        [property: JsonPropertyName("max_completion_tokens")] int? MaxCompletionTokens);

    /// <summary>
    /// Models an OpenAI-compatible SSE chat-completions payload used during streaming.
    /// </summary>
    private sealed record OaiStreamChunk(
        List<OaiChoice>? Choices,
        OaiUsage? Usage);

    /// <summary>
    /// Models an OpenAI-compatible streaming choice carrying delta and finish state.
    /// </summary>
    private sealed record OaiChoice(
        int Index,
        OaiDelta? Delta,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    /// <summary>
    /// Models an OpenAI-compatible streaming delta for text or tool-call updates.
    /// </summary>
    private sealed record OaiDelta(
        string? Role,
        string? Content,
        [property: JsonPropertyName("tool_calls")] List<OaiToolCallDelta>? ToolCalls);

    /// <summary>
    /// Models an OpenAI-compatible streaming tool-call delta and its function data.
    /// </summary>
    private sealed record OaiToolCallDelta(
        int Index,
        string? Id,
        string? Type,
        OaiToolFunction? Function);

    /// <summary>
    /// Models OpenAI-compatible streaming tool function name and argument fragments.
    /// </summary>
    private sealed record OaiToolFunction(string? Name, string? Arguments);

    /// <summary>
    /// Models OpenAI-compatible token usage JSON attached to streaming payloads.
    /// </summary>
    private sealed record OaiUsage(
        [property: JsonPropertyName("prompt_tokens")] int? PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int? CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);
}

/// <summary>
/// Provider-side error wrapping the underlying HTTP failure. Lets the
/// chat-service layer translate to user-friendly messages and decide whether
/// to retry / surface as 500 vs 502.
/// </summary>
public sealed class LlmProviderException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderException"/> class.
    /// </summary>
    /// <param name="providerId">Provider id associated with the failed request.</param>
    /// <param name="statusCode">HTTP status code returned by the provider.</param>
    /// <param name="message">Provider error message.</param>
    public LlmProviderException(string providerId, int statusCode, string message) : base(message)
    {
        ProviderId = providerId;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the provider id value.
    /// </summary>
    public string ProviderId { get; }
    /// <summary>
    /// Gets the status code value.
    /// </summary>
    public int StatusCode { get; }
}
