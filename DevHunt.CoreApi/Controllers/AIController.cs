using DevHunt.CoreApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Ai.Llm;
using DevHunt.CoreApi.Services.Ai.Llm.Providers;
using DevHunt.CoreApi.Services.Ai.Llm.Tools;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Exposes authenticated AI chat, tool execution, cancellation, estimation, and legacy tech-stack endpoints.
/// </summary>
[ApiController]
[Route("api/ai")]
[Authorize]
[RequireFeatureFlag("ai_features")]
public class AIController : ControllerBase
{
    private readonly IMLServiceClient _mlServiceClient;
    private readonly ILlmChatService _llmChat;
    private readonly IAiInFlightRegistry _inFlight;
    private readonly IAiToolExecutionService _toolExecution;
    private readonly ILogger<AIController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIController"/> class.
    /// </summary>
    /// <param name="mlServiceClient">Client used by the legacy ML-backed tech-stack endpoint.</param>
    /// <param name="llmChat">Service that validates, estimates, regenerates, and sends BYOK chat turns.</param>
    /// <param name="inFlight">Registry used to cancel active AI stream requests.</param>
    /// <param name="toolExecution">Service that runs confirmed AI tool calls after authorization checks.</param>
    /// <param name="logger">Logger for authorization and provider failures.</param>
    public AIController(
        IMLServiceClient mlServiceClient,
        ILlmChatService llmChat,
        IAiInFlightRegistry inFlight,
        IAiToolExecutionService toolExecution,
        ILogger<AIController> logger)
    {
        _mlServiceClient = mlServiceClient;
        _llmChat = llmChat;
        _inFlight = inFlight;
        _toolExecution = toolExecution;
        _logger = logger;
    }

    /// <summary>
    /// BYOK LLM chat endpoint. Persists the user's /ai command, streams
    /// provider deltas to the conversation SignalR group and persists the
    /// final assistant response as an AI-generated chat message.
    /// </summary>
    /// <param name="conversationId">Conversation that receives the user prompt and assistant response.</param>
    /// <param name="request">Model, prompt, context, and tool preferences for the chat turn.</param>
    /// <param name="ct">Cancellation token for request shutdown.</param>
    /// <returns>
    /// The persisted chat response; returns 401 without a user claim, 403 for conversation access failures,
    /// 429 with quota metadata for rate limits, 400 for request or validation failures, and 502 for provider errors.
    /// </returns>
    [HttpPost("chat/conversations/{conversationId:guid}")]
    public async Task<IActionResult> SendConversationAiMessage(
        Guid conversationId,
        [FromBody] LlmChatRequestDto request,
        CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var response = await _llmChat.SendConversationMessageAsync(userId.Value, conversationId, request, ct);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized BYOK AI chat request for conversation {ConversationId}", conversationId);
            return Forbid();
        }
        catch (AiRateLimitExceededException ex)
        {
            // 429 with structured payload so the UI can render
            // "X of Y, resets in Zs" without parsing the message.
            Response.Headers["Retry-After"] = ((int)ex.Result.ResetIn.TotalSeconds).ToString();
            return StatusCode(429, new
            {
                error = ex.Message,
                used = ex.Result.Used,
                limit = ex.Result.Limit,
                resetInSeconds = (int)ex.Result.ResetIn.TotalSeconds,
            });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to return.
            return new EmptyResult();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (LlmProviderException ex)
        {
            return StatusCode(502, new { error = ex.Message, provider = ex.ProviderId, statusCode = ex.StatusCode });
        }
    }

    /// <summary>
    /// Replays the current user's latest /ai command in the conversation.
    /// The original command message is reused as the user turn; no duplicate
    /// "/ai ..." chat message is inserted.
    /// </summary>
    /// <param name="conversationId">Conversation containing the command to regenerate.</param>
    /// <param name="request">Regeneration options such as model and context choices.</param>
    /// <param name="ct">Cancellation token for request shutdown.</param>
    /// <returns>
    /// The regenerated response; returns 401 without a user claim, 403 for conversation access failures,
    /// 429 with quota metadata for rate limits, 400 for invalid state, and 502 for provider errors.
    /// </returns>
    [HttpPost("chat/conversations/{conversationId:guid}/regenerate")]
    public async Task<IActionResult> RegenerateConversationAiMessage(
        Guid conversationId,
        [FromBody] LlmRegenerateRequestDto request,
        CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var response = await _llmChat.RegenerateLastConversationMessageAsync(userId.Value, conversationId, request, ct);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized BYOK AI regenerate request for conversation {ConversationId}", conversationId);
            return Forbid();
        }
        catch (AiRateLimitExceededException ex)
        {
            Response.Headers["Retry-After"] = ((int)ex.Result.ResetIn.TotalSeconds).ToString();
            return StatusCode(429, new
            {
                error = ex.Message,
                used = ex.Result.Used,
                limit = ex.Result.Limit,
                resetInSeconds = (int)ex.Result.ResetIn.TotalSeconds,
            });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (LlmProviderException ex)
        {
            return StatusCode(502, new { error = ex.Message, provider = ex.ProviderId, statusCode = ex.StatusCode });
        }
    }

    /// <summary>
    /// Estimates prompt tokens and min/max cost for a BYOK LLM chat turn.
    /// The estimate uses the same model selection, skill parsing, history
    /// policy and project-context assembly as the real streaming endpoint.
    /// </summary>
    /// <param name="conversationId">Conversation whose history and project context are included in the estimate.</param>
    /// <param name="request">Chat request to estimate without sending to the provider.</param>
    /// <param name="ct">Cancellation token for request shutdown.</param>
    /// <returns>The token and cost estimate, or 401, 403, or 400 when identity, access, or request state is invalid.</returns>
    [HttpPost("chat/conversations/{conversationId:guid}/estimate")]
    public async Task<IActionResult> EstimateConversationAiMessage(
        Guid conversationId,
        [FromBody] LlmChatRequestDto request,
        CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var response = await _llmChat.EstimateConversationMessageAsync(userId.Value, conversationId, request, ct);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized BYOK AI estimate request for conversation {ConversationId}", conversationId);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancels an in-flight LLM stream for the current user.
    /// </summary>
    /// <remarks>
    /// The user can fire this from the UI while the assistant is still typing;
    /// the stream stops, partial output is delivered via <c>AiStreamCancelled</c>,
    /// and the user's daily quota is not refunded (we already paid for the network round-trip).
    /// </remarks>
    /// <param name="conversationId">Conversation route segment retained for endpoint scoping.</param>
    /// <param name="requestId">Stream request to cancel for the current user.</param>
    /// <returns>Always returns an OK payload for authenticated users to avoid exposing active request identifiers.</returns>
    [HttpPost("chat/conversations/{conversationId:guid}/cancel/{requestId:guid}")]
    public IActionResult CancelStream(Guid conversationId, Guid requestId)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();

        // The registry already enforces "user A can't cancel user B's stream".
        // We don't 404 either way; returning Ok in both cases avoids giving
        // an attacker a way to enumerate active request ids.
        _inFlight.TryCancel(requestId, userId.Value);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// Executes a batch of confirmed tool calls and records results as a chat message.
    /// </summary>
    /// <remarks>
    /// Each tool runs through <see cref="IAiToolExecutionService"/> with project-level
    /// authorization checks; results are persisted as a single AI-generated chat message
    /// and broadcast as an <c>AiToolsExecuted</c> event so the task board / project view
    /// can refresh in place.
    /// </remarks>
    /// <param name="conversationId">Conversation where the confirmed tool results are recorded.</param>
    /// <param name="request">Confirmed tool calls to execute.</param>
    /// <param name="ct">Cancellation token for tool execution.</param>
    /// <returns>The tool execution response, or 401, 403, or 400 when identity, access, or request content is invalid.</returns>
    [HttpPost("chat/conversations/{conversationId:guid}/tools/execute")]
    public async Task<IActionResult> ExecuteTools(
        Guid conversationId,
        [FromBody] ConfirmToolCallsRequest request,
        CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();
        if (request.ToolCalls is null || request.ToolCalls.Count == 0)
        {
            return BadRequest(new { error = "At least one tool call is required." });
        }

        try
        {
            var response = await _toolExecution.ExecuteAsync(userId.Value, conversationId, request, ct);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized AI tool execution for conversation {ConversationId}", conversationId);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Legacy endpoint for tech stack suggestions.
    /// Redirected to ML service directly for compatibility.
    /// </summary>
    /// <param name="request">Project idea to send to the legacy ML tech-stack suggestion service.</param>
    /// <returns>The generated tech stack, 400 when the idea is missing, 429 for ML rate limits, or 500 for ML service failures.</returns>
    [HttpPost("tech-stack")]
    public async Task<IActionResult> GenerateTechStack([FromBody] LegacyGenerateTechStackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Idea))
            return BadRequest("Idea is required.");

        try
        {
            var result = await _mlServiceClient.GetTechStackSuggestionAsync(request.Idea);
            return Ok(result);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(429, new { error = "AI service rate limit exceeded. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legacy tech-stack generation failed");
            return StatusCode(500, new { error = "AI service error" });
        }
    }
}

/// <summary>
/// Carries the project idea accepted by the legacy tech-stack suggestion endpoint.
/// </summary>
/// <param name="Idea">Free-form product or project idea to analyze.</param>
public record LegacyGenerateTechStackRequest(string Idea);
