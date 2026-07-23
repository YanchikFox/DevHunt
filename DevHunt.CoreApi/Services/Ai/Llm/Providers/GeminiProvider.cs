using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevHunt.CoreApi.Services.Ai.Llm.Models;

namespace DevHunt.CoreApi.Services.Ai.Llm.Providers;

/// <summary>
/// Adapter for Google Gemini's GenerateContent API. Gemini uses its own
/// content/parts shape and sends API keys as query parameters, so it stays
/// separate from the OpenAI-compatible adapter.
/// </summary>
public sealed class GeminiProvider : ILlmProvider
{
    private static readonly Uri BaseUrl = new("https://generativelanguage.googleapis.com/v1beta/");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GeminiProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiProvider"/> class.
    /// </summary>
    /// <param name="httpFactory">Factory for named provider HTTP clients.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public GeminiProvider(IHttpClientFactory httpFactory, ILogger<GeminiProvider> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderId => "gemini";
    /// <inheritdoc />
    public string DisplayName => "Google Gemini";

    /// <inheritdoc />
    public async Task<KeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken ct)
    {
        using var client = CreateHttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"models?key={Uri.EscapeDataString(apiKey)}");

        try
        {
            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return new KeyValidationResult(true);

            var status = (int)resp.StatusCode;
            var preview = await ReadBodyPreviewAsync(resp, ct);
            return status is 400 or 401 or 403
                ? new KeyValidationResult(false, $"Gemini rejected the key (HTTP {status}). {preview}".Trim())
                : new KeyValidationResult(false, $"Gemini returned HTTP {status}. {preview}".Trim());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Gemini validation transport error");
            throw;
        }
    }

    // Model-id fragments that identify non-chat models unsuitable for text generation.
    private static readonly string[] _nonChatFragments =
    [
        "embedding", "embed-",
        "imagen", "veo", "lyria",
        "robotics", "-er-",
        "tts",
    ];

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderModelInfo>> ListModelsAsync(string apiKey, CancellationToken ct)
    {
        using var client = CreateHttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"models?key={Uri.EscapeDataString(apiKey)}");
        using var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<GeminiModelsResponse>(JsonOpts, ct);
        if (payload?.Models == null) return Array.Empty<ProviderModelInfo>();

        return payload.Models
            .Where(m =>
            {
                if (string.IsNullOrWhiteSpace(m.Name)) return false;
                var id = StripModelPrefix(m.Name).ToLowerInvariant();
                // Keep only models that explicitly support generateContent.
                var methods = m.SupportedGenerationMethods;
                if (methods != null && methods.Count > 0)
                    return methods.Contains("generateContent", StringComparer.OrdinalIgnoreCase);
                // Fallback for models with no method list: exclude known non-chat types.
                return !_nonChatFragments.Any(frag => id.Contains(frag, StringComparison.OrdinalIgnoreCase));
            })
            .Select(m =>
            {
                var id = StripModelPrefix(m.Name!).ToLowerInvariant();
                // Capability detection from model id — Gemini API doesn't expose flags directly.
                var supportsTools = id.Contains("gemini") &&
                    !id.Contains("tts") && !id.Contains("image") &&
                    !id.Contains("imagen") && !id.Contains("aqa");
                var supportsVision = id.Contains("gemini") &&
                    !id.Contains("tts") && !id.Contains("embedding");
                return new ProviderModelInfo(
                    StripModelPrefix(m.Name!),
                    m.DisplayName ?? StripModelPrefix(m.Name!),
                    m.InputTokenLimit,
                    SupportsTools: supportsTools,
                    SupportsStreaming: true,
                    SupportsVision: supportsVision,
                    MaxOutputTokens: m.OutputTokenLimit);
            })
            .ToList();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmChatChunk> StreamChatAsync(
        LlmChatRequest request,
        string apiKey,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var client = CreateHttpClient();
        var modelId = request.ModelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? request.ModelId
            : $"models/{request.ModelId}";
        var path = $"{modelId}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(apiKey)}";

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(BuildPayload(request), options: JsonOpts),
        };
        httpReq.Headers.Accept.ParseAdd("text/event-stream");

        using var resp = await client.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var preview = await ReadBodyPreviewAsync(resp, ct);
            throw new LlmProviderException(
                ProviderId,
                (int)resp.StatusCode,
                $"Gemini returned HTTP {(int)resp.StatusCode}. {preview}".Trim());
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);

        // Gemini doesn't issue per-call ids and doesn't index tool calls — we
        // assign a dense 0-based index per call seen in the stream so multiple
        // function calls in one response don't collide on Index=0 in the
        // host's tool-call buffer.
        var nextToolIndex = 0;

        await foreach (var ev in SseEventReader.ReadAsync(stream, ct))
        {
            if (string.IsNullOrWhiteSpace(ev.Data)) continue;

            GeminiGenerateContentResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(ev.Data, JsonOpts);
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Gemini: ignored malformed SSE frame");
                continue;
            }

            if (chunk == null) continue;

            var usage = chunk.UsageMetadata == null
                ? null
                : new LlmUsage(
                    chunk.UsageMetadata.PromptTokenCount,
                    chunk.UsageMetadata.CandidatesTokenCount,
                    chunk.UsageMetadata.TotalTokenCount);

            var candidate = chunk.Candidates?.FirstOrDefault();
            if (candidate == null)
            {
                if (usage != null) yield return new LlmChatChunk(Usage: usage);
                continue;
            }

            var parts = candidate.Content?.Parts ?? [];
            foreach (var part in parts)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return new LlmChatChunk(DeltaText: part.Text, Usage: usage);
                }

                if (part.FunctionCall != null)
                {
                    var toolIdx = nextToolIndex++;
                    yield return new LlmChatChunk(
                        ToolCallDelta: new[]
                        {
                            new LlmToolCallDelta(
                                toolIdx,
                                part.FunctionCall.Name,
                                part.FunctionCall.Name,
                                part.FunctionCall.Args?.GetRawText() ?? "{}"),
                        },
                        Usage: usage);
                }
            }

            var finish = MapFinishReason(candidate.FinishReason);
            if (finish != LlmFinishReason.InProgress)
            {
                yield return new LlmChatChunk(FinishReason: finish, Usage: usage);
            }
        }
    }

    /// <summary>Creates the named Gemini HTTP client with streaming-safe timeout.</summary>
    private HttpClient CreateHttpClient()
    {
        var client = _httpFactory.CreateClient($"llm:{ProviderId}");
        client.BaseAddress = BaseUrl;
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    /// <summary>Builds Gemini GenerateContent payload with system instruction, tools, and generation config.</summary>
    private static object BuildPayload(LlmChatRequest request)
    {
        var systemText = string.Join(
            "\n\n",
            request.Messages
                .Where(m => m.Role == LlmRole.System && !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => m.Content));

        var contents = request.Messages
            .Where(m => m.Role != LlmRole.System)
            .Select(BuildContent)
            .ToArray();

        return new
        {
            systemInstruction = string.IsNullOrWhiteSpace(systemText)
                ? null
                : new { parts = new[] { new { text = systemText } } },
            contents,
            tools = request.Tools is { Count: > 0 }
                ? new[]
                {
                    new
                    {
                        functionDeclarations = request.Tools.Select(t => new
                        {
                            name = t.Name,
                            description = t.Description,
                            parameters = t.ParametersSchema,
                        }).ToArray(),
                    },
                }
                : null,
            generationConfig = new
            {
                temperature = request.Temperature,
                topP = request.TopP,
                maxOutputTokens = request.MaxOutputTokens,
                stopSequences = request.StopSequences,
            },
        };
    }

    /// <summary>Maps canonical messages and tool exchanges to Gemini content parts.</summary>
    private static object BuildContent(LlmMessage message)
    {
        if (message.Role == LlmRole.Tool)
        {
            JsonElement response;
            try
            {
                response = JsonDocument.Parse(string.IsNullOrWhiteSpace(message.Content) ? "{}" : message.Content).RootElement.Clone();
            }
            catch (JsonException)
            {
                response = JsonDocument.Parse("{}").RootElement.Clone();
            }

            return new
            {
                role = "user",
                parts = new[]
                {
                    new
                    {
                        functionResponse = new
                        {
                            name = message.Name ?? message.ToolCallId ?? "tool",
                            response,
                        },
                    },
                },
            };
        }

        if (message.Role == LlmRole.Assistant && message.ToolCalls is { Count: > 0 })
        {
            var parts = new List<object>();
            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                parts.Add(new { text = message.Content });
            }

            foreach (var call in message.ToolCalls)
            {
                JsonElement args;
                try
                {
                    args = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson).RootElement.Clone();
                }
                catch (JsonException)
                {
                    args = JsonDocument.Parse("{}").RootElement.Clone();
                }

                parts.Add(new
                {
                    functionCall = new
                    {
                        name = call.Name,
                        args,
                    },
                });
            }

            return new { role = "model", parts };
        }

        return new
        {
            role = message.Role == LlmRole.Assistant ? "model" : "user",
            parts = new[] { new { text = message.Content ?? string.Empty } },
        };
    }

    /// <summary>Removes the <c>models/</c> prefix returned by Gemini model listing.</summary>
    private static string StripModelPrefix(string modelName) =>
        modelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelName["models/".Length..]
            : modelName;

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

    /// <summary>Normalizes Gemini finish reasons to <see cref="LlmFinishReason"/>.</summary>
    private static LlmFinishReason MapFinishReason(string? reason) => reason switch
    {
        null => LlmFinishReason.InProgress,
        "STOP" => LlmFinishReason.Stop,
        "MAX_TOKENS" => LlmFinishReason.Length,
        "SAFETY" or "RECITATION" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII" => LlmFinishReason.ContentFilter,
        _ => LlmFinishReason.Other,
    };

    /// <summary>
    /// Models the Gemini JSON payload returned during model listing.
    /// </summary>
    private sealed record GeminiModelsResponse(List<GeminiModel>? Models);

    /// <summary>
    /// Models a Gemini JSON model entry used during model listing.
    /// </summary>
    private sealed record GeminiModel(
        string? Name,
        string? DisplayName,
        int? InputTokenLimit,
        int? OutputTokenLimit,
        List<string>? SupportedGenerationMethods);

    /// <summary>
    /// Models a Gemini SSE GenerateContent payload used during streaming.
    /// </summary>
    private sealed record GeminiGenerateContentResponse(
        List<GeminiCandidate>? Candidates,
        GeminiUsageMetadata? UsageMetadata);

    /// <summary>
    /// Models a Gemini streaming candidate carrying response content and finish state.
    /// </summary>
    private sealed record GeminiCandidate(
        GeminiContent? Content,
        string? FinishReason);

    /// <summary>
    /// Models Gemini streaming content parts returned in a candidate.
    /// </summary>
    private sealed record GeminiContent(List<GeminiPart>? Parts);

    /// <summary>
    /// Models a Gemini streaming part containing text or a function call.
    /// </summary>
    private sealed record GeminiPart(
        string? Text,
        GeminiFunctionCall? FunctionCall);

    /// <summary>
    /// Models a Gemini streaming function-call payload and its JSON arguments.
    /// </summary>
    private sealed record GeminiFunctionCall(
        string? Name,
        JsonElement? Args);

    /// <summary>
    /// Models Gemini token usage metadata attached to streaming payloads.
    /// </summary>
    private sealed record GeminiUsageMetadata(
        int? PromptTokenCount,
        int? CandidatesTokenCount,
        int? TotalTokenCount);
}
