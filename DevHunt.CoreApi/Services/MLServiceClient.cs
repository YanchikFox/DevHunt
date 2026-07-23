using System.Net.Http.Json;
using DevHunt.CoreApi.Services.Ai;
using Microsoft.Extensions.Configuration;

namespace DevHunt.CoreApi.Services;

/// <summary>
/// HTTP client for ML Service integration (FastAPI backend).
/// Corresponds to architecture: Core API -> ML Service (gRPC/HTTP).
/// </summary>
/// <remarks>
/// <para><strong>ML Service Capabilities</strong>:</para>
/// - Project recommendations for users (collaborative filtering + content-based)
/// - AI planning (tech stack suggestions and phased plans)
/// - Embedding generation (text vectorization for semantic search)
///
/// <para><strong>Communication Protocol</strong>:</para>
/// Currently uses HTTP/JSON (simpler for MVP).
/// Can be migrated to gRPC for better performance in production (lower latency, smaller payloads).
///
/// <para><strong>Graceful Degradation</strong>:</para>
/// If ML Service is unavailable or times out, returns empty results instead of throwing exceptions.
/// This prevents ML failures from breaking core platform functionality.
/// </remarks>
public interface IMLServiceClient
{
    /// <summary>Fetches ranked project recommendations for a user from the ML service.</summary>
    /// <param name="userId">Target user ID.</param>
    /// <param name="limit">Maximum number of recommendations to return.</param>
    /// <returns>Recommendation list; empty when the service is disabled or the request fails.</returns>
    Task<List<MLRecommendationDto>> GetRecommendationsForUserAsync(Guid userId, int limit = 10);
    /// <summary>Asks the ML service to rebuild recommendation models in the background.</summary>
    /// <returns>A completed task; failures are logged and not rethrown.</returns>
    Task TriggerRecommendationsRefreshAsync();

    // Legacy AI helpers
    /// <summary>Legacy wrapper that returns tech-stack options for a project idea.</summary>
    /// <param name="idea">Project idea or brief description.</param>
    /// <returns>Tech-stack options from <see cref="GenerateTechStackAsync"/>; may be empty on failure.</returns>
    Task<List<AITechStackOptionDto>> GetTechStackSuggestionAsync(string idea);
    /// <summary>Legacy helper that requests phased roadmap suggestions for an idea and stack.</summary>
    /// <param name="idea">Project idea.</param>
    /// <param name="techStack">Selected technology stack.</param>
    /// <returns>Roadmap phases from the ML service.</returns>
    Task<List<AIRoadmapPhaseDto>> GetRoadmapAsync(string idea, string techStack);
    /// <summary>Legacy helper that requests task suggestions for a roadmap phase.</summary>
    /// <param name="idea">Project idea.</param>
    /// <param name="phase">Roadmap phase name.</param>
    /// <param name="techStack">Selected technology stack.</param>
    /// <returns>Suggested tasks for the phase.</returns>
    Task<List<AITaskItemDto>> GetTasksAsync(string idea, string phase, string techStack);

    // New AI planning endpoints
    /// <summary>Calls <c>/api/ai/generate-tech-stack</c> with optional goal weightings.</summary>
    /// <param name="idea">Project idea.</param>
    /// <param name="goals">Optional scoring weights for stack trade-offs.</param>
    /// <returns>Ranked tech-stack options and token usage metadata.</returns>
    Task<AITechStackResponseDto> GenerateTechStackAsync(string idea, TechStackGoals? goals = null);
    /// <summary>Calls <c>/api/ai/generate-plan</c> to produce a phased project plan.</summary>
    /// <param name="idea">Project idea.</param>
    /// <param name="techStack">Chosen stack.</param>
    /// <param name="customTags">Optional extra tags to include.</param>
    /// <param name="customRoles">Optional open roles to include.</param>
    /// <param name="goals">Optional goal weightings forwarded to the ML service.</param>
    /// <returns>Draft plan with phases and tasks.</returns>
    Task<AiPlanDraftDto> GeneratePlanAsync(
        string idea,
        string techStack,
        List<string>? customTags = null,
        List<string>? customRoles = null,
        TechStackGoals? goals = null);

    // Diagram generation
    /// <summary>Calls <c>/api/ai/generate-diagram</c> to produce architecture or flow diagrams.</summary>
    /// <param name="techStack">Technology stack to depict.</param>
    /// <param name="idea">Optional project idea for context.</param>
    /// <param name="format">Output format, default <c>mermaid</c>.</param>
    /// <param name="diagramType">Diagram kind, default <c>architecture</c>.</param>
    /// <param name="projectContext">Optional serialized project context.</param>
    /// <returns>Generated diagram code and format.</returns>
    Task<AiDiagramResponseDto> GenerateDiagramAsync(string techStack, string? idea = null, string format = "mermaid", string diagramType = "architecture", string? projectContext = null);

    // Passport generation
    /// <summary>Calls <c>/api/ai/generate-passport</c> to build a structured project passport document.</summary>
    /// <param name="idea">Project idea.</param>
    /// <param name="techStack">Chosen stack.</param>
    /// <param name="description">Optional long description.</param>
    /// <param name="phases">Optional phase summaries from an existing plan.</param>
    /// <param name="language">Output language code, default <c>en</c>.</param>
    /// <param name="documentsContext">Optional uploaded document context.</param>
    /// <returns>Passport sections and model metadata.</returns>
    Task<AiPassportResponseDto> GeneratePassportAsync(
        string idea,
        string techStack,
        string? description = null,
        List<AiPassportPhaseDto>? phases = null,
        string language = "en",
        string? documentsContext = null);

    // Refinement endpoints
    /// <summary>Calls <c>/api/ai/refine-plan</c> to revise an existing plan from natural-language instructions.</summary>
    /// <param name="idea">Project idea.</param>
    /// <param name="techStack">Chosen stack.</param>
    /// <param name="currentPlan">Existing plan object sent back to the model.</param>
    /// <param name="instructions">User refinement instructions.</param>
    /// <param name="customTags">Optional extra tags.</param>
    /// <param name="customRoles">Optional open roles.</param>
    /// <param name="goals">Optional goal weightings.</param>
    /// <returns>Revised draft plan.</returns>
    Task<AiPlanDraftDto> RefinePlanAsync(
        string idea,
        string techStack,
        object currentPlan,
        string instructions,
        List<string>? customTags = null,
        List<string>? customRoles = null,
        TechStackGoals? goals = null);
    /// <summary>Calls <c>/api/ai/refine-tech-stack</c> to adjust stack options from instructions.</summary>
    /// <param name="idea">Project idea.</param>
    /// <param name="currentOptions">Current stack options object.</param>
    /// <param name="instructions">User refinement instructions.</param>
    /// <param name="goals">Optional goal weightings.</param>
    /// <returns>Revised tech-stack response.</returns>
    Task<AITechStackResponseDto> RefineTechStackAsync(
        string idea,
        object currentOptions,
        string instructions,
        TechStackGoals? goals = null);

    // Embedding generation for RAG
    /// <summary>Calls <c>/api/embeddings/generate</c> for batch text vectorization used by RAG.</summary>
    /// <param name="texts">Texts to embed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Embedding vectors; empty list when disabled, input is empty, or the call fails.</returns>
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default);
    /// <summary>Calls <c>/api/embeddings/query</c> for a single search-query vector.</summary>
    /// <param name="query">Natural-language query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Query embedding, or <see langword="null"/> when disabled or on failure.</returns>
    Task<float[]?> GenerateQueryEmbeddingAsync(string query, CancellationToken ct = default);
}

/// <summary>
/// HTTP client implementation for the ML Service.
/// </summary>
public class MLServiceClient : IMLServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MLServiceClient> _logger;
    private readonly bool _isEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="MLServiceClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with the ML service base URL.</param>
    /// <param name="logger">Logger for request failures and disabled-service warnings.</param>
    /// <param name="configuration">Reads <c>MLService:BaseUrl</c> and optional <c>MLService:ServiceToken</c>.</param>
    public MLServiceClient(
        HttpClient httpClient,
        ILogger<MLServiceClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;

        var mlServiceUrl = configuration["MLService:BaseUrl"] ?? "http://ml-service:8000";
        _httpClient.BaseAddress = new Uri(mlServiceUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);

        _isEnabled = !string.IsNullOrEmpty(configuration["MLService:BaseUrl"]);

        // REC-09: Service-to-service auth — pass pre-shared token so ML service can verify the caller
        var serviceToken = configuration["MLService:ServiceToken"];
        if (!string.IsNullOrEmpty(serviceToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceToken);
        }

        if (!_isEnabled)
        {
            _logger.LogWarning("ML Service disabled (MLService:BaseUrl not configured)");
        }
    }

    /// <inheritdoc />
    public async Task<List<MLRecommendationDto>> GetRecommendationsForUserAsync(Guid userId, int limit = 10)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("ML Service disabled, returning empty recommendations for user {UserId}", userId);
            return new List<MLRecommendationDto>();
        }

        try
        {
            var response = await _httpClient.GetAsync($"/api/recommendations/user/{userId}?limit={limit}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ML Service returned {StatusCode} for user {UserId}", response.StatusCode, userId);
                return new List<MLRecommendationDto>();
            }

            var recommendations = await response.Content.ReadFromJsonAsync<List<MLRecommendationDto>>();
            return recommendations ?? new List<MLRecommendationDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get recommendations from ML Service for user {UserId}", userId);
            return new List<MLRecommendationDto>();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "ML Service request timeout for user {UserId}", userId);
            return new List<MLRecommendationDto>();
        }
    }

    /// <inheritdoc />
    public async Task TriggerRecommendationsRefreshAsync()
    {
        if (!_isEnabled) return;
        try
        {
            await _httpClient.PostAsync("/api/recommendations/refresh", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger recommendations refresh in ML Service");
        }
    }

    /// <inheritdoc />
    public async Task<List<AITechStackOptionDto>> GetTechStackSuggestionAsync(string idea)
    {
        var response = await GenerateTechStackAsync(idea);
        return response.Options ?? new List<AITechStackOptionDto>();
    }

    /// <inheritdoc />
    public async Task<List<AIRoadmapPhaseDto>> GetRoadmapAsync(string idea, string techStack)
    {
        return await CallAiEndpointAsync<List<AIRoadmapPhaseDto>>("/api/ai/roadmap", new { idea, techStack });
    }

    /// <inheritdoc />
    public async Task<List<AITaskItemDto>> GetTasksAsync(string idea, string phase, string techStack)
    {
        return await CallAiEndpointAsync<List<AITaskItemDto>>("/api/ai/tasks", new { idea, phase, techStack });
    }

    /// <inheritdoc />
    public async Task<AITechStackResponseDto> GenerateTechStackAsync(string idea, TechStackGoals? goals = null)
    {
        return await CallAiEndpointAsync<AITechStackResponseDto>("/api/ai/generate-tech-stack", new
        {
            idea,
            goals = goals is not null ? new
            {
                performance = goals.Performance,
                cost = goals.Cost,
                developerSpeed = goals.DeveloperSpeed,
                scalability = goals.Scalability,
                security = goals.Security,
                maintainability = goals.Maintainability
            } : null
        });
    }

    /// <inheritdoc />
    public async Task<AiPlanDraftDto> GeneratePlanAsync(
        string idea,
        string techStack,
        List<string>? customTags = null,
        List<string>? customRoles = null,
        TechStackGoals? goals = null)
    {
        return await CallAiEndpointAsync<AiPlanDraftDto>("/api/ai/generate-plan", new
        {
            idea,
            techStack,
            customTags,
            customRoles,
            goals = goals is not null ? new
            {
                performance = goals.Performance,
                cost = goals.Cost,
                developerSpeed = goals.DeveloperSpeed,
                scalability = goals.Scalability,
                security = goals.Security,
                maintainability = goals.Maintainability
            } : null
        });
    }

    /// <inheritdoc />
    public async Task<AiPlanDraftDto> RefinePlanAsync(
        string idea,
        string techStack,
        object currentPlan,
        string instructions,
        List<string>? customTags = null,
        List<string>? customRoles = null,
        TechStackGoals? goals = null)
    {
        return await CallAiEndpointAsync<AiPlanDraftDto>("/api/ai/refine-plan", new
        {
            idea,
            techStack,
            currentPlan,
            instructions,
            customTags,
            customRoles,
            goals = goals is not null ? new
            {
                performance = goals.Performance,
                cost = goals.Cost,
                developerSpeed = goals.DeveloperSpeed,
                scalability = goals.Scalability,
                security = goals.Security,
                maintainability = goals.Maintainability
            } : null
        });
    }

    /// <inheritdoc />
    public async Task<AiDiagramResponseDto> GenerateDiagramAsync(string techStack, string? idea = null, string format = "mermaid", string diagramType = "architecture", string? projectContext = null)
    {
        return await CallAiEndpointAsync<AiDiagramResponseDto>("/api/ai/generate-diagram", new
        {
            techStack,
            idea,
            format,
            diagramType,
            projectContext
        });
    }

    /// <inheritdoc />
    public async Task<AiPassportResponseDto> GeneratePassportAsync(
        string idea,
        string techStack,
        string? description = null,
        List<AiPassportPhaseDto>? phases = null,
        string language = "en",
        string? documentsContext = null)
    {
        return await CallAiEndpointAsync<AiPassportResponseDto>("/api/ai/generate-passport", new
        {
            idea,
            techStack,
            description,
            phases = phases?.Select(p => new
            {
                name = p.Name,
                description = p.Description,
                goals = p.Goals,
                taskCount = p.TaskCount
            }),
            language,
            documentsContext
        });
    }

    /// <inheritdoc />
    public async Task<AITechStackResponseDto> RefineTechStackAsync(
        string idea,
        object currentOptions,
        string instructions,
        TechStackGoals? goals = null)
    {
        return await CallAiEndpointAsync<AITechStackResponseDto>("/api/ai/refine-tech-stack", new
        {
            idea,
            currentOptions,
            instructions,
            goals = goals is not null ? new
            {
                performance = goals.Performance,
                cost = goals.Cost,
                developerSpeed = goals.DeveloperSpeed,
                scalability = goals.Scalability,
                security = goals.Security,
                maintainability = goals.Maintainability
            } : null
        });
    }

    /// <summary>
    /// POSTs JSON to an AI endpoint and deserializes the response.
    /// Throws <see cref="InvalidOperationException"/> when the service is disabled or the body is empty;
    /// throws <see cref="HttpRequestException"/> with status code only on non-success responses.
    /// </summary>
    private async Task<T> CallAiEndpointAsync<T>(string endpoint, object payload)
    {
        if (!_isEnabled)
        {
            throw new InvalidOperationException("ML Service is disabled.");
        }

        var response = await _httpClient.PostAsJsonAsync(endpoint, payload);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<T>();
            if (result == null)
            {
                throw new InvalidOperationException($"AI Service returned empty body for {endpoint}");
            }
            return result;
        }

        // REC-07: Log Injection — truncate external body, never include in exception message
        var rawBody = await response.Content.ReadAsStringAsync();
        var safeSnippet = rawBody.Length > 200 ? rawBody[..200] + "…" : rawBody;
        _logger.LogWarning("AI Service returned {StatusCode} for {Endpoint}. Body snippet: {Snippet}",
            response.StatusCode, endpoint, safeSnippet);

        // Exception carries only status code — no external content that could inject log events
        throw new HttpRequestException(
            $"AI Service returned {(int)response.StatusCode} {response.StatusCode} for {endpoint}");
    }

    // ── Embedding methods for RAG ──

    /// <inheritdoc />
    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken ct = default)
    {
        if (!_isEnabled || texts.Count == 0) return [];

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/embeddings/generate", new { texts }, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<EmbeddingsResponse>(ct);
            return result?.Embeddings ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embeddings for {Count} texts", texts.Count);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<float[]?> GenerateQueryEmbeddingAsync(string query, CancellationToken ct = default)
    {
        if (!_isEnabled) return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/embeddings/query", new { query }, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<QueryEmbeddingResponse>(ct);
            return result?.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate query embedding");
            return null;
        }
    }

    /// <summary>
    /// Represents a batch embedding response from the ML service.
    /// </summary>
    private record EmbeddingsResponse(List<float[]> Embeddings, string? Model, int? Dimensions);

    /// <summary>
    /// Represents a single query embedding response from the ML service.
    /// </summary>
    private record QueryEmbeddingResponse(float[] Embedding, string? Model, int? Dimensions);
}

/// <summary>
/// DTO for ML Service recommendation response.
/// </summary>
public record MLRecommendationDto(
    Guid ProjectId,
    decimal MatchScore,
    Dictionary<string, object>? Reasoning
);

// --- AI DTOs ---

/// <summary>Single tech-stack alternative returned by the ML planning API.</summary>
/// <param name="Name">Stack or framework name.</param>
/// <param name="Description">Short rationale for the option.</param>
/// <param name="Pros">Advantages listed by the model.</param>
/// <param name="Cons">Trade-offs listed by the model.</param>
public record AITechStackOptionDto(
    string Name,
    string Description,
    List<string> Pros,
    List<string> Cons
);

/// <summary>Tech-stack generation response including model metadata and ranked options.</summary>
/// <param name="Version">Response schema version.</param>
/// <param name="Options">Ranked stack alternatives.</param>
/// <param name="PromptVersion">Prompt template version used by the ML service.</param>
/// <param name="Provider">LLM provider name.</param>
/// <param name="Model">Model identifier.</param>
/// <param name="PromptTokens">Prompt token count reported by the provider.</param>
/// <param name="CompletionTokens">Completion token count reported by the provider.</param>
/// <param name="TotalTokens">Total token count reported by the provider.</param>
public record AITechStackResponseDto(
    string Version,
    List<AITechStackOptionDto> Options,
    string? PromptVersion = null,
    string? Provider = null,
    string? Model = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null
);

/// <summary>One roadmap phase suggested for a project idea.</summary>
/// <param name="Phase">Phase label returned by the planning prompt.</param>
/// <param name="Description">Narrative explanation of the work grouped into the phase.</param>
/// <param name="Goals">Milestones the generated phase is meant to achieve.</param>
public record AIRoadmapPhaseDto(
    string Phase,
    string Description,
    List<string> Goals
);

/// <summary>Task suggestion returned for a roadmap phase.</summary>
/// <param name="Title">Suggested task title from the model response.</param>
/// <param name="Description">Model-generated implementation context for the task.</param>
/// <param name="Dependencies">Other generated tasks that should precede this one.</param>
public record AITaskItemDto(
    string Title,
    string Description,
    List<string> Dependencies
);

/// <summary>Full AI-generated project plan draft with phases and token usage metadata.</summary>
/// <param name="Version">Response schema version understood by the API.</param>
/// <param name="Idea">Original project idea that the generated plan expands.</param>
/// <param name="TechStack">Tech stack selected or echoed by the planning model.</param>
/// <param name="Phases">Generated implementation phases and their tasks.</param>
/// <param name="PromptVersion">Prompt template version used by the ML service.</param>
/// <param name="Provider">LLM provider that produced the draft, when reported.</param>
/// <param name="Model">Model identifier that produced the draft, when reported.</param>
/// <param name="PromptTokens">Prompt token count reported by the provider.</param>
/// <param name="CompletionTokens">Completion token count reported by the provider.</param>
/// <param name="TotalTokens">Total token count reported by the provider.</param>
public record AiPlanDraftDto(
    string Version,
    string Idea,
    string TechStack,
    List<AiPlanPhaseDto> Phases,
    string? PromptVersion = null,
    string? Provider = null,
    string? Model = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null
);

/// <summary>Phase within an AI-generated project plan.</summary>
/// <param name="Id">Stable phase identifier used by generated task dependencies.</param>
/// <param name="Name">Display name for the generated phase.</param>
/// <param name="Description">Model-provided explanation of the phase scope.</param>
/// <param name="Goals">Outcomes the phase should complete.</param>
/// <param name="Tasks">Tasks grouped under this phase.</param>
public record AiPlanPhaseDto(
    string Id,
    string Name,
    string Description,
    List<string> Goals,
    List<AiPlanTaskDto> Tasks
);

/// <summary>Task entry inside an AI-generated plan phase.</summary>
/// <param name="Id">Generated task identifier referenced by dependency lists.</param>
/// <param name="Title">Task title intended for project planning surfaces.</param>
/// <param name="Description">Implementation detail generated for the task.</param>
/// <param name="DependsOn">Generated task identifiers that must be completed first.</param>
/// <param name="Priority">Priority label assigned by the plan generator.</param>
/// <param name="Tags">Optional labels inferred for filtering or grouping the task.</param>
public record AiPlanTaskDto(
    string Id,
    string Title,
    string Description,
    List<string> DependsOn,
    string Priority = "medium",
    List<string>? Tags = null
);

/// <summary>Generated diagram payload from the ML service.</summary>
/// <param name="Code">Diagram source returned by the model.</param>
/// <param name="Format">Renderer format for the diagram source; defaults to Mermaid.</param>
public record AiDiagramResponseDto(
    string Code,
    string? Format = "mermaid"
);

// --- Passport DTOs ---

/// <summary>Phase summary included when generating a project passport.</summary>
/// <param name="Name">Plan phase name copied into the passport prompt.</param>
/// <param name="Description">Phase summary supplied to the passport generator.</param>
/// <param name="Goals">Goals associated with the phase in the source plan.</param>
/// <param name="TaskCount">Number of tasks in the phase, used as passport context.</param>
public record AiPassportPhaseDto(
    string Name,
    string Description,
    List<string> Goals,
    int TaskCount = 0
);

/// <summary>Structured section within a generated project passport.</summary>
/// <param name="Type">Machine-readable section category returned by the ML service.</param>
/// <param name="Title">Human-readable heading for the generated section.</param>
/// <param name="Content">Markdown or plain text body generated for the section.</param>
public record AiPassportSectionDto(
    string Type,
    string Title,
    string Content
);

/// <summary>Project passport document returned by the ML service.</summary>
/// <param name="Sections">Generated passport sections in display order.</param>
/// <param name="Provider">LLM provider that generated the passport, when reported.</param>
/// <param name="Model">Model identifier that generated the passport, when reported.</param>
/// <param name="PromptTokens">Prompt token count reported by the provider.</param>
/// <param name="CompletionTokens">Completion token count reported by the provider.</param>
/// <param name="TotalTokens">Total token count reported by the provider.</param>
public record AiPassportResponseDto(
    List<AiPassportSectionDto> Sections,
    string? Provider = null,
    string? Model = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null
);
