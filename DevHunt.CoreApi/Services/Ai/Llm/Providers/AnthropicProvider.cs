using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevHunt.CoreApi.Services.Ai.Llm.Models;

namespace DevHunt.CoreApi.Services.Ai.Llm.Providers;

/// <summary>
/// Adapter for Anthropic's <c>/v1/messages</c> API. Anthropic diverges from
/// OpenAI in three important ways and this class is where we hide them:
///   1. <c>system</c> is a top-level field, not a message role.
///   2. Tool calls are <c>content_block</c>s of type <c>tool_use</c>, with
///      arguments streamed as <c>input_json_delta</c> fragments.
///   3. Content-block index != tool-call index - we maintain a remap so the
///      canonical <see cref="LlmToolCallDelta.Index"/> is dense and 0-based.
/// </summary>
public sealed class AnthropicProvider : ILlmProvider
{
    private const string AnthropicVersion = "2023-06-01";
    private static readonly Uri BaseUrl = new("https://api.anthropic.com/v1/");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AnthropicProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicProvider"/> class.
    /// </summary>
    /// <param name="httpFactory">Factory for named provider HTTP clients.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public AnthropicProvider(IHttpClientFactory httpFactory, ILogger<AnthropicProvider> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderId => "anthropic";
    /// <inheritdoc />
    public string DisplayName => "Anthropic";

    /// <inheritdoc />
    public async Task<KeyValidationResult> ValidateKeyAsync(string apiKey, CancellationToken ct)
    {
        using var client = CreateHttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "models");
        ApplyHeaders(req, apiKey);

        try
        {
            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return new KeyValidationResult(true);

            var status = (int)resp.StatusCode;
            var preview = await ReadBodyPreviewAsync(resp, ct);
            return status is 401 or 403
                ? new KeyValidationResult(false, $"Anthropic rejected the key (HTTP {status}). {preview}".Trim())
                : new KeyValidationResult(false, $"Anthropic returned HTTP {status}. {preview}".Trim());
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Anthropic validation transport error");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderModelInfo>> ListModelsAsync(string apiKey, CancellationToken ct)
    {
        using var client = CreateHttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "models");
        ApplyHeaders(req, apiKey);

        using var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<AnthropicModelsResponse>(JsonOpts, ct);
        if (payload?.Data == null) return Array.Empty<ProviderModelInfo>();

        return payload.Data
            .Where(m => !string.IsNullOrWhiteSpace(m.Id))
            .Select(m => new ProviderModelInfo(
                m.Id!,
                m.DisplayName ?? m.Id,
                SupportsTools: true,
                SupportsStreaming: true,
                SupportsVision: true))
            .ToList();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmChatChunk> StreamChatAsync(
        LlmChatRequest request,
        string apiKey,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var client = CreateHttpClient();
        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "messages")
        {
            Content = JsonContent.Create(BuildPayload(request), options: JsonOpts),
        };
        ApplyHeaders(httpReq, apiKey);
        httpReq.Headers.Accept.ParseAdd("text/event-stream");

        using var resp = await client.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var preview = await ReadBodyPreviewAsync(resp, ct);
            throw new LlmProviderException(
                ProviderId,
                (int)resp.StatusCode,
                $"Anthropic returned HTTP {(int)resp.StatusCode}. {preview}".Trim());
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);

        // Anthropic numbers content blocks per-message and mixes text + tool_use
        // freely. The canonical model wants tool calls indexed densely, so we
        // remap content_block_index to tool_call_index here.
        var contentBlockToToolIndex = new Dictionary<int, int>();
        var nextToolIndex = 0;
        var pendingFinish = LlmFinishReason.InProgress;
        int? promptTokens = null;
        int? completionTokens = null;

        await foreach (var ev in SseEventReader.ReadAsync(stream, ct))
        {
            if (string.IsNullOrWhiteSpace(ev.Data)) continue;

            switch (ev.EventType)
            {
                case "ping":
                    continue;

                case "message_start":
                {
                    var msg = TryParse<AnthropicMessageStart>(ev.Data);
                    if (msg?.Message?.Usage?.InputTokens is int it) promptTokens = it;
                    break;
                }

                case "content_block_start":
                {
                    var start = TryParse<AnthropicContentBlockStart>(ev.Data);
                    if (start?.ContentBlock?.Type == "tool_use")
                    {
                        var toolIdx = nextToolIndex++;
                        contentBlockToToolIndex[start.Index] = toolIdx;
                        // Emit the tool-call header (id + name) immediately so the
                        // host can show "calling foo(...)" while args still stream.
                        yield return new LlmChatChunk(
                            ToolCallDelta: new[]
                            {
                                new LlmToolCallDelta(toolIdx, start.ContentBlock.Id, start.ContentBlock.Name),
                            });
                    }
                    break;
                }

                case "content_block_delta":
                {
                    var delta = TryParse<AnthropicContentBlockDelta>(ev.Data);
                    if (delta?.Delta == null) break;

                    if (delta.Delta.Type == "text_delta" && delta.Delta.Text != null)
                    {
                        yield return new LlmChatChunk(DeltaText: delta.Delta.Text);
                    }
                    else if (delta.Delta.Type == "input_json_delta" && delta.Delta.PartialJson != null)
                    {
                        if (contentBlockToToolIndex.TryGetValue(delta.Index, out var toolIdx))
                        {
                            yield return new LlmChatChunk(
                                ToolCallDelta: new[]
                                {
                                    new LlmToolCallDelta(toolIdx, ArgumentsJsonFragment: delta.Delta.PartialJson),
                                });
                        }
                    }
                    break;
                }

                case "message_delta":
                {
                    var msgDelta = TryParse<AnthropicMessageDelta>(ev.Data);
                    if (msgDelta?.Delta?.StopReason is string stop)
                    {
                        pendingFinish = MapFinishReason(stop);
                    }
                    if (msgDelta?.Usage?.OutputTokens is int ot) completionTokens = ot;
                    break;
                }

                case "message_stop":
                {
                    var total = (promptTokens ?? 0) + (completionTokens ?? 0);
                    yield return new LlmChatChunk(
                        FinishReason: pendingFinish == LlmFinishReason.InProgress ? LlmFinishReason.Stop : pendingFinish,
                        Usage: new LlmUsage(promptTokens, completionTokens, total > 0 ? total : null));
                    yield break;
                }

                case "error":
                {
                    var err = TryParse<AnthropicError>(ev.Data);
                    throw new LlmProviderException(
                        ProviderId,
                        500,
                        err?.Error?.Message ?? "Anthropic stream returned an error event.");
                }
            }
        }
    }

    // --- internals --------------------------------------------------------

    /// <summary>Creates the named Anthropic HTTP client with streaming-safe timeout.</summary>
    private HttpClient CreateHttpClient()
    {
        var client = _httpFactory.CreateClient($"llm:{ProviderId}");
        client.BaseAddress = BaseUrl;
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    /// <summary>Applies Anthropic API key and version headers.</summary>
    private static void ApplyHeaders(HttpRequestMessage req, string apiKey)
    {
        req.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        req.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
    }

    /// <summary>Deserializes provider JSON and returns default on malformed frames.</summary>
    private static T? TryParse<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch (JsonException) { return default; }
    }

    /// <summary>Reads a bounded response-body preview for validation and error messages.</summary>
    private static async Task<string> ReadBodyPreviewAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            return body.Length > 300 ? body[..300] + "..." : body;
        }
        catch { return string.Empty; }
    }

    /// <summary>Builds Anthropic Messages payload, lifting system turns to the top-level system field.</summary>
    private object BuildPayload(LlmChatRequest request)
    {
        // Anthropic separates system from messages. Concatenate any system
        // turns the canonical model carries; everything else maps 1:1.
        var systemTexts = request.Messages
            .Where(m => m.Role == LlmRole.System && !string.IsNullOrEmpty(m.Content))
            .Select(m => m.Content!)
            .ToList();

        var systemPrompt = systemTexts.Count == 0
            ? null
            : string.Join("\n\n", systemTexts);

        var nonSystem = request.Messages
            .Where(m => m.Role != LlmRole.System)
            .Select(BuildMessage)
            .ToArray();

        return new
        {
            model = request.ModelId,
            // max_tokens is required by Anthropic - fall back to a sane default
            // matching the smaller Sonnet/Haiku output ceilings.
            max_tokens = request.MaxOutputTokens ?? 4096,
            system = systemPrompt,
            messages = nonSystem,
            tools = request.Tools is { Count: > 0 }
                ? request.Tools.Select(BuildTool).ToArray()
                : null,
            temperature = request.Temperature,
            top_p = request.TopP,
            stop_sequences = request.StopSequences,
            stream = true,
        };
    }

    /// <summary>Maps canonical user, assistant, and tool-result messages to Anthropic content blocks.</summary>
    private static object BuildMessage(LlmMessage m)
    {
        switch (m.Role)
        {
            case LlmRole.Tool:
                // A tool result lives inside a user-role turn as a tool_result block.
                return new
                {
                    role = "user",
                    content = new[]
                    {
                        new
                        {
                            type = "tool_result",
                            tool_use_id = m.ToolCallId,
                            content = m.Content ?? string.Empty,
                        },
                    },
                };

            case LlmRole.Assistant when m.ToolCalls is { Count: > 0 }:
            {
                var blocks = new List<object>();
                if (!string.IsNullOrEmpty(m.Content))
                {
                    blocks.Add(new { type = "text", text = m.Content });
                }
                foreach (var tc in m.ToolCalls)
                {
                    // Anthropic wants the parsed JSON object, not the string.
                    JsonElement input;
                    try
                    {
                        input = JsonDocument.Parse(string.IsNullOrWhiteSpace(tc.ArgumentsJson) ? "{}" : tc.ArgumentsJson).RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        input = JsonDocument.Parse("{}").RootElement.Clone();
                    }
                    blocks.Add(new
                    {
                        type = "tool_use",
                        id = tc.Id,
                        name = tc.Name,
                        input,
                    });
                }
                return new { role = "assistant", content = blocks };
            }

            default:
                return new
                {
                    role = m.Role == LlmRole.User ? "user" : "assistant",
                    content = m.Content ?? string.Empty,
                };
        }
    }

    /// <summary>Maps a canonical tool definition to Anthropic input schema format.</summary>
    private static object BuildTool(LlmToolDefinition t) => new
    {
        name = t.Name,
        description = t.Description,
        input_schema = t.ParametersSchema,
    };

    /// <summary>Normalizes Anthropic stop reasons to <see cref="LlmFinishReason"/>.</summary>
    private static LlmFinishReason MapFinishReason(string stop) => stop switch
    {
        "end_turn" or "stop_sequence" => LlmFinishReason.Stop,
        "max_tokens" => LlmFinishReason.Length,
        "tool_use" => LlmFinishReason.ToolCalls,
        _ => LlmFinishReason.Other,
    };

    // Wire DTOs.
    /// <summary>
    /// Models the Anthropic JSON payload returned during model listing.
    /// </summary>
    private sealed record AnthropicModelsResponse(List<AnthropicModel>? Data);

    /// <summary>
    /// Models an Anthropic JSON model entry used during model listing.
    /// </summary>
    private sealed record AnthropicModel(
        string? Id,
        [property: JsonPropertyName("display_name")] string? DisplayName);

    /// <summary>
    /// Models an Anthropic SSE message_start payload used during streaming.
    /// </summary>
    private sealed record AnthropicMessageStart(AnthropicMessageStartBody? Message);

    /// <summary>
    /// Models the Anthropic message_start body that carries streaming usage data.
    /// </summary>
    private sealed record AnthropicMessageStartBody(AnthropicUsage? Usage);

    /// <summary>
    /// Models an Anthropic SSE content_block_start payload used during streaming.
    /// </summary>
    private sealed record AnthropicContentBlockStart(int Index, AnthropicContentBlock? ContentBlock);

    /// <summary>
    /// Models an Anthropic streaming content block for text or tool-use starts.
    /// </summary>
    private sealed record AnthropicContentBlock(string? Type, string? Id, string? Name);

    /// <summary>
    /// Models an Anthropic SSE content_block_delta payload used during streaming.
    /// </summary>
    private sealed record AnthropicContentBlockDelta(int Index, AnthropicDelta? Delta);

    /// <summary>
    /// Models an Anthropic streaming delta for text or partial tool-call JSON.
    /// </summary>
    private sealed record AnthropicDelta(string? Type, string? Text, [property: JsonPropertyName("partial_json")] string? PartialJson);

    /// <summary>
    /// Models an Anthropic SSE message_delta payload used during streaming.
    /// </summary>
    private sealed record AnthropicMessageDelta(AnthropicMessageDeltaBody? Delta, AnthropicUsage? Usage);

    /// <summary>
    /// Models the Anthropic message_delta body that carries streaming stop reasons.
    /// </summary>
    private sealed record AnthropicMessageDeltaBody(
        [property: JsonPropertyName("stop_reason")] string? StopReason);

    /// <summary>
    /// Models Anthropic token usage JSON attached to streaming events.
    /// </summary>
    private sealed record AnthropicUsage(
        [property: JsonPropertyName("input_tokens")] int? InputTokens,
        [property: JsonPropertyName("output_tokens")] int? OutputTokens);

    /// <summary>
    /// Models an Anthropic SSE error payload used during streaming.
    /// </summary>
    private sealed record AnthropicError(AnthropicErrorBody? Error);

    /// <summary>
    /// Models the Anthropic error body returned in a streaming error event.
    /// </summary>
    private sealed record AnthropicErrorBody(string? Type, string? Message);
}
