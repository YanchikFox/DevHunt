using System.Text.Json;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.CodeAnalysis;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Internal API for code analysis results (used by integration-gateway).
/// Stores and retrieves analysis results from the DevHunt Analyzer service.
///
/// Routes: api/internal/code-analysis/*
/// </summary>
[ApiController]
[Route("api/internal/code-analysis")]
[Authorize]
public class CodeAnalysisInternalController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly IInternalServiceAuthenticator _serviceAuth;
    private readonly IMLServiceClient _mlService;
    private readonly ICodeAnalysisEmbeddingJobService _embeddingJobs;
    private readonly ILogger<CodeAnalysisInternalController> _logger;

    /// <summary>
    /// Creates the internal code-analysis controller with storage, cache, service-authentication, ML, and logging services.
    /// </summary>
    /// <param name="db">Database context used to save analyzer results and embeddings.</param>
    /// <param name="cache">Cache service used to invalidate latest-analysis entries.</param>
    /// <param name="serviceAuth">Authenticator that validates internal integration-gateway calls.</param>
    /// <param name="mlService">ML service client used to generate embeddings for analysis issues.</param>
    /// <param name="embeddingJobs">Durable queue for post-analysis embedding generation.</param>
    /// <param name="logger">Logger for ingestion and embedding diagnostics.</param>
    public CodeAnalysisInternalController(
        DevHuntDbContext db,
        ICacheService cache,
        IInternalServiceAuthenticator serviceAuth,
        IMLServiceClient mlService,
        ICodeAnalysisEmbeddingJobService embeddingJobs,
        ILogger<CodeAnalysisInternalController> logger)
    {
        _db = db;
        _cache = cache;
        _serviceAuth = serviceAuth;
        _mlService = mlService;
        _embeddingJobs = embeddingJobs;
        _logger = logger;
    }

    #region DTOs

    /// <summary>
    /// Payload posted by the integration gateway after an analyzer run completes.
    /// </summary>
    public record SaveAnalysisResultRequest
    {
        /// <summary>
        /// Integration that produced the analysis result.
        /// </summary>
        public Guid IntegrationId { get; init; }
        /// <summary>
        /// Project whose repository was analyzed.
        /// </summary>
        public Guid ProjectId { get; init; }
        /// <summary>
        /// Repository name or URL analyzed by the analyzer service.
        /// </summary>
        public string Repository { get; init; } = string.Empty;
        /// <summary>
        /// Branch name analyzed, when available.
        /// </summary>
        public string? Branch { get; init; }
        /// <summary>
        /// Commit SHA analyzed, when available.
        /// </summary>
        public string? CommitSha { get; init; }
        /// <summary>
        /// Total number of issues reported by the analyzer.
        /// </summary>
        public int TotalIssues { get; init; }
        /// <summary>
        /// Total number of files included in the analyzer result.
        /// </summary>
        public int TotalFiles { get; init; }
        /// <summary>
        /// Analyzer runtime in milliseconds.
        /// </summary>
        public int AnalysisTimeMs { get; init; }
        /// <summary>
        /// Issue counts grouped by severity.
        /// </summary>
        public Dictionary<string, int>? SeverityCounts { get; init; }
        /// <summary>
        /// Issue counts grouped by category.
        /// </summary>
        public Dictionary<string, int>? CategoryCounts { get; init; }
        /// <summary>
        /// Raw issue payloads returned by the analyzer.
        /// </summary>
        public List<object>? Issues { get; init; }
    }

    #endregion

    /// <summary>
    /// Saves a completed analyzer result from the integration gateway and starts embedding generation in the background.
    /// </summary>
    [HttpPost("results")]
    [AllowAnonymous] // Internal service call — validated via IInternalServiceAuthenticator
    public async Task<IActionResult> SaveResult([FromBody] SaveAnalysisResultRequest req, CancellationToken ct = default)
    {
        if (!IsInternalServiceCall()) return Forbid();

        // Validate project exists
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == req.ProjectId, ct);
        if (!projectExists) return NotFound(new { error = "Project not found" });

        var result = new CodeAnalysisResult
        {
            Id = Guid.NewGuid(),
            ProjectId = req.ProjectId,
            IntegrationId = req.IntegrationId,
            Repository = req.Repository,
            Branch = req.Branch,
            CommitSha = req.CommitSha,
            TotalIssues = req.TotalIssues,
            TotalFiles = req.TotalFiles,
            AnalysisTimeMs = req.AnalysisTimeMs,
            SeverityCountsJson = req.SeverityCounts != null
                ? JsonSerializer.Serialize(req.SeverityCounts)
                : null,
            CategoryCountsJson = req.CategoryCounts != null
                ? JsonSerializer.Serialize(req.CategoryCounts)
                : null,
            IssuesJson = req.Issues != null
                ? JsonSerializer.Serialize(req.Issues)
                : null,
            Status = "completed",
            CreatedAt = DateTime.UtcNow,
        };

        _db.CodeAnalysisResults.Add(result);
        await _db.SaveChangesAsync(ct);

        // Invalidate cache
        await _cache.RemoveAsync($"code-analysis:project:{req.ProjectId}:latest");

        _logger.LogInformation(
            "Saved code analysis result {ResultId} for project {ProjectId}: {TotalIssues} issues in {TotalFiles} files",
            result.Id, req.ProjectId, req.TotalIssues, req.TotalFiles);

        var embeddingJobId = await _embeddingJobs.EnqueueAsync(result.Id, req.ProjectId, ct);

        return Created(
            $"/api/internal/code-analysis/results/{result.Id}",
            new { result.Id, embeddingJobId });
    }

    /// <summary>
    /// Returns durable embedding job status for observability (internal services only).
    /// </summary>
    [HttpGet("embedding-jobs/{jobId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEmbeddingJobStatus(Guid jobId, CancellationToken ct = default)
    {
        if (!IsInternalServiceCall())
        {
            return Forbid();
        }

        var status = await _embeddingJobs.GetStatusAsync(jobId, ct);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>
    /// Validates that the request is signed as an internal service call for the current path.
    /// </summary>
    private bool IsInternalServiceCall()
    {
        var requestPath = HttpContext.Request.Path.Value ?? "";
        return _serviceAuth.ValidateRequest(HttpContext, requestPath);
    }
}

/// <summary>
/// Public API for code analysis results (accessible by project members).
/// Routes: api/projects/{projectId}/code-analysis/*
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/code-analysis")]
[Authorize]
public class CodeAnalysisController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly IMLServiceClient _mlService;
    private readonly ILogger<CodeAnalysisController> _logger;

    /// <summary>
    /// Creates the public code-analysis controller with storage, cache, ML, and logging services.
    /// </summary>
    /// <param name="db">Database context used to read analysis results and update analysis configuration.</param>
    /// <param name="cache">Cache service reserved for analysis response caching.</param>
    /// <param name="mlService">ML service client used for query and issue embeddings.</param>
    /// <param name="logger">Logger for embedding generation diagnostics.</param>
    public CodeAnalysisController(
        DevHuntDbContext db,
        ICacheService cache,
        IMLServiceClient mlService,
        ILogger<CodeAnalysisController> logger)
    {
        _db = db;
        _cache = cache;
        _mlService = mlService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the latest completed analysis result for a project member.
    /// </summary>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(Guid projectId, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        // Verify the user is a member of the project
        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var result = await _db.CodeAnalysisResults
            .Where(r => r.ProjectId == projectId && r.Status == "completed")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (result == null) return NotFound(new { message = "No analysis results found" });

        return Ok(MapToResponse(result));
    }

    /// <summary>
    /// Returns recent analysis history for a project member, clamping the requested limit to 1 through 50.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(Guid projectId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var cappedLimit = Math.Clamp(limit, 1, 50);

        var results = await _db.CodeAnalysisResults
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(cappedLimit)
            .ToListAsync(ct);

        return Ok(results.Select(MapToResponse));
    }

    /// <summary>
    /// Returns a specific analysis result when it belongs to the requested project and the caller is a member.
    /// </summary>
    [HttpGet("results/{resultId:guid}")]
    public async Task<IActionResult> GetResult(Guid projectId, Guid resultId, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var result = await _db.CodeAnalysisResults
            .FirstOrDefaultAsync(r => r.Id == resultId && r.ProjectId == projectId, ct);

        if (result == null) return NotFound();

        return Ok(MapToResponse(result));
    }

    #region AI-Optimized Endpoints (Summary + Search)

    /// <summary>
    /// Compact issue summary for the latest analysis result.
    /// </summary>
    public record IssueSummaryResponse
    {
        /// <summary>
        /// Total issues reported by the analyzer before dismissals.
        /// </summary>
        public int TotalIssues { get; init; }
        /// <summary>
        /// Total files included in the analyzer run.
        /// </summary>
        public int TotalFiles { get; init; }
        /// <summary>
        /// Issues remaining after dismissed issues are excluded.
        /// </summary>
        public int ActiveIssues { get; init; }
        /// <summary>
        /// Number of dismissed issues found in integration configuration.
        /// </summary>
        public int DismissedCount { get; init; }
        /// <summary>
        /// Issue counts grouped by severity.
        /// </summary>
        public Dictionary<string, int> SeverityCounts { get; init; } = new();
        /// <summary>
        /// Issue counts grouped by category.
        /// </summary>
        public Dictionary<string, int> CategoryCounts { get; init; } = new();
        /// <summary>
        /// Most frequent rules ordered by severity and count.
        /// </summary>
        public List<RuleGroupSummary> TopRules { get; init; } = [];
        /// <summary>
        /// Files with the highest active issue counts.
        /// </summary>
        public List<FileSummary> WorstFiles { get; init; } = [];
        /// <summary>
        /// Branch analyzed by the latest run, when available.
        /// </summary>
        public string? Branch { get; init; }
        /// <summary>
        /// Commit SHA analyzed by the latest run, when available.
        /// </summary>
        public string? CommitSha { get; init; }
        /// <summary>
        /// Timestamp when the latest analysis result was created.
        /// </summary>
        public DateTime? AnalyzedAt { get; init; }
    }

    /// <summary>
    /// Summary of issues grouped by analyzer rule.
    /// </summary>
    public record RuleGroupSummary
    {
        /// <summary>
        /// Analyzer rule identifier.
        /// </summary>
        public string RuleId { get; init; } = string.Empty;
        /// <summary>
        /// Human-readable analyzer rule name, when available.
        /// </summary>
        public string? RuleName { get; init; }
        /// <summary>
        /// Severity assigned to the rule group.
        /// </summary>
        public string Severity { get; init; } = string.Empty;
        /// <summary>
        /// Analyzer category assigned to the rule group.
        /// </summary>
        public string Category { get; init; } = string.Empty;
        /// <summary>
        /// Number of active issues for the rule.
        /// </summary>
        public int Count { get; init; }
        /// <summary>
        /// Representative analyzer message for the rule.
        /// </summary>
        public string? SampleMessage { get; init; }
    }

    /// <summary>
    /// Summary of issue volume for one file.
    /// </summary>
    public record FileSummary
    {
        /// <summary>
        /// File path reported by the analyzer.
        /// </summary>
        public string File { get; init; } = string.Empty;
        /// <summary>
        /// Number of active issues in the file.
        /// </summary>
        public int IssueCount { get; init; }
    }

    /// <summary>
    /// Paginated raw issue search response.
    /// </summary>
    public record IssueSearchResponse
    {
        /// <summary>
        /// Total issues matching the supplied filters.
        /// </summary>
        public int Total { get; init; }
        /// <summary>
        /// Number of issues returned in this page.
        /// </summary>
        public int Returned { get; init; }
        /// <summary>
        /// Offset used after clamping negative values to zero.
        /// </summary>
        public int Offset { get; init; }
        /// <summary>
        /// Raw issue JSON elements returned for this page.
        /// </summary>
        public List<JsonElement> Issues { get; init; } = [];
    }

    /// <summary>
    /// Returns a compact latest-analysis summary for project members, excluding dismissed issues from active counts.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(Guid projectId, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var result = await _db.CodeAnalysisResults
            .Where(r => r.ProjectId == projectId && r.Status == "completed")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (result == null) return NotFound(new { message = "No analysis results found" });

        var severityCounts = result.SeverityCountsJson != null
            ? JsonSerializer.Deserialize<Dictionary<string, int>>(result.SeverityCountsJson) ?? new()
            : new Dictionary<string, int>();
        var categoryCounts = result.CategoryCountsJson != null
            ? JsonSerializer.Deserialize<Dictionary<string, int>>(result.CategoryCountsJson) ?? new()
            : new Dictionary<string, int>();

        // Parse issues to compute top rules and worst files
        var topRules = new List<RuleGroupSummary>();
        var worstFiles = new List<FileSummary>();
        int dismissedCount = 0;

        if (result.IssuesJson != null)
        {
            var issues = JsonSerializer.Deserialize<List<JsonElement>>(result.IssuesJson) ?? [];

            // Get dismissed issues from integration config
            var integration = await _db.Integrations
                .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IsActive, ct);
            var dismissed = new HashSet<string>();
            if (integration != null)
            {
                var config = ParseConfig(integration.ConfigJson);
                var dismissedList = GetDismissedFromConfig(config);
                foreach (var d in dismissedList)
                    dismissed.Add($"{d.RuleId}|{d.File}|{d.Line}");
            }

            // Group by rule_id → top 10 rules
            var ruleGroups = new Dictionary<string, (string? ruleName, string severity, string category, string? message, int count)>();
            var fileCounts = new Dictionary<string, int>();

            foreach (var issue in issues)
            {
                var ruleId = issue.TryGetProperty("rule_id", out var rid) ? rid.GetString() ?? "" : "";
                var file = issue.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? ""
                         : issue.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "";
                var line = issue.TryGetProperty("line", out var ln) ? ln.GetInt32().ToString() : "";

                // Check dismissed
                if (dismissed.Contains($"{ruleId}|{file}|{line}") ||
                    dismissed.Contains($"{ruleId}||"))
                {
                    dismissedCount++;
                    continue;
                }

                // Accumulate rule group
                if (ruleGroups.TryGetValue(ruleId, out var existing))
                {
                    ruleGroups[ruleId] = (existing.ruleName, existing.severity, existing.category, existing.message, existing.count + 1);
                }
                else
                {
                    var ruleName = issue.TryGetProperty("rule_name", out var rn) ? rn.GetString() : null;
                    var severity = issue.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "info" : "info";
                    var category = issue.TryGetProperty("category", out var ct2) ? ct2.GetString() ?? "" : "";
                    var message = issue.TryGetProperty("message", out var msg) ? msg.GetString() : null;
                    ruleGroups[ruleId] = (ruleName, severity, category, message, 1);
                }

                // Accumulate file counts
                if (!string.IsNullOrEmpty(file))
                {
                    fileCounts[file] = fileCounts.GetValueOrDefault(file) + 1;
                }
            }

            // Top 10 rules by count (severity-first, then count)
            var sevOrder = new Dictionary<string, int>
            {
                ["critical"] = 0, ["high"] = 1, ["medium"] = 2, ["low"] = 3, ["info"] = 4,
            };
            topRules = ruleGroups
                .OrderBy(r => sevOrder.GetValueOrDefault(r.Value.severity, 5))
                .ThenByDescending(r => r.Value.count)
                .Take(15)
                .Select(r => new RuleGroupSummary
                {
                    RuleId = r.Key,
                    RuleName = r.Value.ruleName,
                    Severity = r.Value.severity,
                    Category = r.Value.category,
                    Count = r.Value.count,
                    SampleMessage = r.Value.message,
                })
                .ToList();

            // Top 10 worst files
            worstFiles = fileCounts
                .OrderByDescending(f => f.Value)
                .Take(10)
                .Select(f => new FileSummary { File = f.Key, IssueCount = f.Value })
                .ToList();
        }

        return Ok(new IssueSummaryResponse
        {
            TotalIssues = result.TotalIssues,
            TotalFiles = result.TotalFiles,
            ActiveIssues = result.TotalIssues - dismissedCount,
            DismissedCount = dismissedCount,
            SeverityCounts = severityCounts,
            CategoryCounts = categoryCounts,
            TopRules = topRules,
            WorstFiles = worstFiles,
            Branch = result.Branch,
            CommitSha = result.CommitSha,
            AnalyzedAt = result.CreatedAt,
        });
    }

    /// <summary>
    /// Searches raw latest-analysis issues by rule, file, severity, category, and free text for project members.
    /// </summary>
    [HttpGet("issues")]
    public async Task<IActionResult> SearchIssues(
        Guid projectId,
        [FromQuery] string? ruleId = null,
        [FromQuery] string? file = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? category = null,
        [FromQuery] string? q = null,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var result = await _db.CodeAnalysisResults
            .Where(r => r.ProjectId == projectId && r.Status == "completed")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (result?.IssuesJson == null)
            return Ok(new IssueSearchResponse());

        var allIssues = JsonSerializer.Deserialize<List<JsonElement>>(result.IssuesJson) ?? [];

        // Apply filters
        IEnumerable<JsonElement> filtered = allIssues;

        if (!string.IsNullOrEmpty(ruleId))
        {
            filtered = filtered.Where(i =>
                i.TryGetProperty("rule_id", out var r) && r.GetString() == ruleId);
        }

        if (!string.IsNullOrEmpty(severity))
        {
            filtered = filtered.Where(i =>
                i.TryGetProperty("severity", out var s) &&
                string.Equals(s.GetString(), severity, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(category))
        {
            filtered = filtered.Where(i =>
                i.TryGetProperty("category", out var c) &&
                string.Equals(c.GetString(), category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(file))
        {
            var fileLower = file.ToLowerInvariant();
            filtered = filtered.Where(i =>
            {
                var f = i.TryGetProperty("file_path", out var fp) ? fp.GetString()
                      : i.TryGetProperty("file", out var ff) ? ff.GetString() : null;
                return f != null && f.ToLowerInvariant().Contains(fileLower);
            });
        }

        if (!string.IsNullOrEmpty(q))
        {
            var qLower = q.ToLowerInvariant();
            filtered = filtered.Where(i =>
            {
                var message = i.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                var ruleName = i.TryGetProperty("rule_name", out var rn) ? rn.GetString() ?? "" : "";
                var rId = i.TryGetProperty("rule_id", out var ri) ? ri.GetString() ?? "" : "";
                var filePath = i.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? ""
                             : i.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "";
                return message.ToLowerInvariant().Contains(qLower)
                    || ruleName.ToLowerInvariant().Contains(qLower)
                    || rId.ToLowerInvariant().Contains(qLower)
                    || filePath.ToLowerInvariant().Contains(qLower);
            });
        }

        var cappedLimit = Math.Clamp(limit, 1, 100);
        var cappedOffset = Math.Max(offset, 0);

        var materialised = filtered.ToList();
        var paged = materialised.Skip(cappedOffset).Take(cappedLimit).ToList();

        return Ok(new IssueSearchResponse
        {
            Total = materialised.Count,
            Returned = paged.Count,
            Offset = cappedOffset,
            Issues = paged,
        });
    }

    /// <summary>
    /// Runs semantic search over latest analysis embeddings and returns relevant rule groups for a natural-language query.
    /// </summary>
    [HttpGet("semantic-search")]
    public async Task<IActionResult> SemanticSearch(
        Guid projectId,
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required" });

        // Generate embedding for the query
        var queryEmbedding = await _mlService.GenerateQueryEmbeddingAsync(q, ct);
        if (queryEmbedding == null)
            return StatusCode(503, new { error = "Embedding service unavailable" });

        // Search using pgvector cosine distance
        var cappedLimit = Math.Clamp(limit, 1, 30);
        var vectorString = "[" + string.Join(",", queryEmbedding) + "]";

        var results = await _db.Database
            .SqlQueryRaw<SemanticSearchRow>(
                """
                SELECT "Id", "RuleId", "RuleName", "Severity", "Category",
                       "IssueCount", "SampleMessage", "TopFilesJson", "CweId",
                       "SampleSuggestion", "EmbeddingText",
                       ("Embedding" <=> @p0::vector) AS "Distance"
                FROM "CodeAnalysisEmbeddings"
                WHERE "ProjectId" = @p1
                  AND "AnalysisResultId" = (
                      SELECT "Id" FROM "CodeAnalysisResults"
                      WHERE "ProjectId" = @p1 AND "Status" = 'completed'
                      ORDER BY "CreatedAt" DESC LIMIT 1
                  )
                ORDER BY "Embedding" <=> @p0::vector
                LIMIT @p2
                """,
                vectorString, projectId, cappedLimit)
            .ToListAsync(ct);

        return Ok(new
        {
            query = q,
            results = results.Select(r => new
            {
                r.RuleId,
                r.RuleName,
                r.Severity,
                r.Category,
                r.IssueCount,
                r.SampleMessage,
                r.CweId,
                r.SampleSuggestion,
                TopFiles = r.TopFilesJson != null
                    ? JsonSerializer.Deserialize<List<string>>(r.TopFilesJson)
                    : null,
                Similarity = 1.0 - r.Distance,
            }),
        });
    }

    /// <summary>
    /// Generates embeddings for the latest analysis result unless embeddings already exist for that result.
    /// </summary>
    [HttpPost("generate-embeddings")]
    public async Task<IActionResult> GenerateEmbeddings(Guid projectId, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var result = await _db.CodeAnalysisResults
            .Where(r => r.ProjectId == projectId && r.Status == "completed")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (result?.IssuesJson == null)
            return NotFound(new { error = "No analysis results found" });

        // Check if embeddings already exist for this result
        var existingCount = await _db.CodeAnalysisEmbeddings
            .CountAsync(e => e.AnalysisResultId == result.Id, ct);
        if (existingCount > 0)
            return Ok(new { message = "Embeddings already generated", count = existingCount });

        var issues = JsonSerializer.Deserialize<List<JsonElement>>(result.IssuesJson) ?? [];

        // Group issues by rule_id
        var ruleGroups = new Dictionary<string, RuleEmbeddingData>();
        foreach (var issue in issues)
        {
            var ruleId = issue.TryGetProperty("rule_id", out var rid) ? rid.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(ruleId)) continue;

            if (!ruleGroups.TryGetValue(ruleId, out var group))
            {
                group = new RuleEmbeddingData
                {
                    RuleName = issue.TryGetProperty("rule_name", out var rn) ? rn.GetString() : null,
                    Severity = issue.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "info" : "info",
                    Category = issue.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "",
                    Message = issue.TryGetProperty("message", out var msg) ? msg.GetString() : null,
                    CweId = issue.TryGetProperty("cwe_id", out var cwe) ? cwe.GetString() : null,
                    Suggestion = issue.TryGetProperty("suggestion", out var sug) ? sug.GetString() : null,
                };
                ruleGroups[ruleId] = group;
            }

            group.Count++;
            var file = issue.TryGetProperty("file_path", out var fp) ? fp.GetString()
                     : issue.TryGetProperty("file", out var f) ? f.GetString() : null;
            if (file != null) group.Files.Add(file);
        }

        // Build embedding texts
        var groupList = ruleGroups.ToList();
        var texts = groupList.Select(g =>
        {
            var topFiles = g.Value.Files.GroupBy(f => f).OrderByDescending(x => x.Count()).Take(5).Select(x => x.Key);
            return $"{g.Value.RuleName ?? g.Key}: {g.Value.Message}. " +
                   $"Category: {g.Value.Category}. Severity: {g.Value.Severity}. " +
                   $"Found {g.Value.Count} times. " +
                   (g.Value.CweId != null ? $"CWE: {g.Value.CweId}. " : "") +
                   $"Files: {string.Join(", ", topFiles)}";
        }).ToList();

        // Generate embeddings via ml-service
        var embeddings = await _mlService.GenerateEmbeddingsAsync(texts, ct);
        if (embeddings.Count == 0)
            return StatusCode(503, new { error = "Embedding service unavailable" });

        // Replace this project's embeddings atomically (DEV-111): the previous RemoveRange was
        // never flushed (no SaveChanges), so stale rows survived and accumulated duplicates that
        // poisoned vector search. ExecuteDeleteAsync runs immediately; the transaction makes the
        // delete + raw INSERTs all-or-nothing.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await _db.CodeAnalysisEmbeddings
            .Where(e => e.ProjectId == projectId)
            .ExecuteDeleteAsync(ct);

        // Save new embeddings using raw SQL (EF Core doesn't natively support vector type)
        for (var i = 0; i < groupList.Count && i < embeddings.Count; i++)
        {
            var g = groupList[i];
            var topFiles = g.Value.Files.GroupBy(f => f).OrderByDescending(x => x.Count()).Take(5).Select(x => x.Key).ToList();
            var vectorStr = "[" + string.Join(",", embeddings[i]) + "]";
            var topFilesJson = JsonSerializer.Serialize(topFiles);

            await _db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "CodeAnalysisEmbeddings"
                    ("Id", "AnalysisResultId", "ProjectId", "RuleId", "RuleName",
                     "Severity", "Category", "IssueCount", "EmbeddingText",
                     "SampleMessage", "TopFilesJson", "CweId", "SampleSuggestion",
                     "Embedding", "CreatedAt")
                VALUES
                    (gen_random_uuid(), @p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9::jsonb, @p10, @p11,
                     @p12::vector, now())
                """,
                result.Id, projectId, g.Key, g.Value.RuleName ?? g.Key,
                g.Value.Severity, g.Value.Category, g.Value.Count, texts[i],
                g.Value.Message, topFilesJson, g.Value.CweId, g.Value.Suggestion,
                vectorStr);
        }

        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Generated {Count} embeddings for project {ProjectId} analysis {ResultId}",
            groupList.Count, projectId, result.Id);

        return Ok(new { message = "Embeddings generated", count = groupList.Count });
    }

    /// <summary>Mutable grouping state used while converting raw issues into embedding rows.</summary>
    private class RuleEmbeddingData
    {
        /// <summary>
        /// Human-readable analyzer rule name, when available.
        /// </summary>
        public string? RuleName { get; set; }
        /// <summary>
        /// Severity assigned to grouped issues.
        /// </summary>
        public string Severity { get; set; } = "info";
        /// <summary>
        /// Analyzer category assigned to grouped issues.
        /// </summary>
        public string Category { get; set; } = "";
        /// <summary>
        /// Representative analyzer message for the rule.
        /// </summary>
        public string? Message { get; set; }
        /// <summary>
        /// CWE identifier associated with the rule, when available.
        /// </summary>
        public string? CweId { get; set; }
        /// <summary>
        /// Representative remediation suggestion, when available.
        /// </summary>
        public string? Suggestion { get; set; }
        /// <summary>
        /// Number of issues in the rule group.
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// File paths where the rule was reported.
        /// </summary>
        public List<string> Files { get; set; } = [];
    }

    // Raw SQL result for semantic search
    /// <summary>Raw SQL projection returned by pgvector semantic search.</summary>
    private class SemanticSearchRow
    {
        /// <summary>
        /// Embedding row identifier.
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// Analyzer rule identifier.
        /// </summary>
        public string RuleId { get; set; } = "";
        /// <summary>
        /// Human-readable analyzer rule name, when available.
        /// </summary>
        public string? RuleName { get; set; }
        /// <summary>
        /// Severity assigned to the rule group.
        /// </summary>
        public string Severity { get; set; } = "";
        /// <summary>
        /// Analyzer category assigned to the rule group.
        /// </summary>
        public string Category { get; set; } = "";
        /// <summary>
        /// Number of issues represented by the embedding row.
        /// </summary>
        public int IssueCount { get; set; }
        /// <summary>
        /// Representative analyzer message for the rule.
        /// </summary>
        public string? SampleMessage { get; set; }
        /// <summary>
        /// JSON array of the most frequent file paths for the rule.
        /// </summary>
        public string? TopFilesJson { get; set; }
        /// <summary>
        /// CWE identifier associated with the rule, when available.
        /// </summary>
        public string? CweId { get; set; }
        /// <summary>
        /// Representative remediation suggestion, when available.
        /// </summary>
        public string? SampleSuggestion { get; set; }
        /// <summary>
        /// Text that was embedded for semantic search.
        /// </summary>
        public string? EmbeddingText { get; set; }
        /// <summary>
        /// Vector distance returned by pgvector for the current query.
        /// </summary>
        public double Distance { get; set; }
    }

    #endregion

    #region Analysis Config (exclude patterns, dismissed issues)

    /// <summary>
    /// Analysis configuration visible to project members.
    /// </summary>
    public record AnalysisConfigResponse
    {
        /// <summary>
        /// File patterns excluded from analyzer runs.
        /// </summary>
        public List<string> ExcludePatterns { get; init; } = [];
        /// <summary>
        /// Issues the project has dismissed from active summaries.
        /// </summary>
        public List<DismissedIssueDto> DismissedIssues { get; init; } = [];
    }

    /// <summary>
    /// Persisted dismissal for one analyzer issue or rule.
    /// </summary>
    public record DismissedIssueDto
    {
        /// <summary>
        /// Analyzer rule identifier that was dismissed.
        /// </summary>
        public string RuleId { get; init; } = string.Empty;
        /// <summary>
        /// Optional file path for a file-specific dismissal.
        /// </summary>
        public string? File { get; init; }
        /// <summary>
        /// Optional line number for a line-specific dismissal.
        /// </summary>
        public int? Line { get; init; }
        /// <summary>
        /// Optional dismissal reason supplied by the user.
        /// </summary>
        public string? Reason { get; init; }
        /// <summary>
        /// Display name or identifier of the user who dismissed the issue.
        /// </summary>
        public string? DismissedBy { get; init; }
        /// <summary>
        /// Timestamp when the issue was dismissed.
        /// </summary>
        public DateTime DismissedAt { get; init; }
    }

    /// <summary>
    /// Request payload that replaces analysis exclude patterns.
    /// </summary>
    public record UpdateExcludePatternsRequest
    {
        /// <summary>
        /// File patterns to store in the active integration configuration.
        /// </summary>
        public List<string> ExcludePatterns { get; init; } = [];
    }

    /// <summary>
    /// Request payload that dismisses one analyzer issue or rule.
    /// </summary>
    public record DismissIssueRequest
    {
        /// <summary>
        /// Analyzer rule identifier to dismiss.
        /// </summary>
        public string RuleId { get; init; } = string.Empty;
        /// <summary>
        /// Optional file path for a file-specific dismissal.
        /// </summary>
        public string? File { get; init; }
        /// <summary>
        /// Optional line number for a line-specific dismissal.
        /// </summary>
        public int? Line { get; init; }
        /// <summary>
        /// Optional dismissal reason stored with the configuration.
        /// </summary>
        public string? Reason { get; init; }
    }

    /// <summary>
    /// Request payload that removes one dismissed analyzer issue or rule.
    /// </summary>
    public record UndismissIssueRequest
    {
        /// <summary>
        /// Analyzer rule identifier to re-open.
        /// </summary>
        public string RuleId { get; init; } = string.Empty;
        /// <summary>
        /// Optional file path for a file-specific dismissal.
        /// </summary>
        public string? File { get; init; }
        /// <summary>
        /// Optional line number for a line-specific dismissal.
        /// </summary>
        public int? Line { get; init; }
    }

    /// <summary>
    /// Returns exclude patterns and dismissed issues from the active integration configuration for project members.
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetAnalysisConfig(Guid projectId, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var integration = await _db.Integrations
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IsActive, ct);

        if (integration == null)
            return Ok(new AnalysisConfigResponse());

        var config = ParseConfig(integration.ConfigJson);
        return Ok(new AnalysisConfigResponse
        {
            ExcludePatterns = GetListFromConfig(config, "excludePatterns"),
            DismissedIssues = GetDismissedFromConfig(config),
        });
    }

    /// <summary>
    /// Replaces exclude patterns in the active integration configuration for project members.
    /// </summary>
    [HttpPatch("config/exclude-patterns")]
    public async Task<IActionResult> UpdateExcludePatterns(
        Guid projectId, [FromBody] UpdateExcludePatternsRequest req, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var integration = await _db.Integrations
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IsActive, ct);
        if (integration == null) return NotFound(new { error = "No active integration found" });

        var config = ParseConfig(integration.ConfigJson);
        config["excludePatterns"] = JsonSerializer.SerializeToElement(req.ExcludePatterns);
        integration.ConfigJson = JsonSerializer.Serialize(config);
        integration.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Exclude patterns updated", excludePatterns = req.ExcludePatterns });
    }

    /// <summary>
    /// Adds an issue dismissal to the active integration configuration unless the same issue is already dismissed.
    /// </summary>
    [HttpPost("dismiss")]
    public async Task<IActionResult> DismissIssue(
        Guid projectId, [FromBody] DismissIssueRequest req, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var integration = await _db.Integrations
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IsActive, ct);
        if (integration == null) return NotFound(new { error = "No active integration found" });

        var config = ParseConfig(integration.ConfigJson);
        var dismissed = GetDismissedFromConfig(config);

        // Check for duplicate
        var alreadyDismissed = dismissed.Any(d =>
            d.RuleId == req.RuleId && d.File == req.File && d.Line == req.Line);
        if (alreadyDismissed)
            return Ok(new { message = "Issue already dismissed" });

        // Get user display name
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        dismissed.Add(new DismissedIssueDto
        {
            RuleId = req.RuleId,
            File = req.File,
            Line = req.Line,
            Reason = req.Reason,
            DismissedBy = user?.FullName ?? user?.Username ?? userId.Value.ToString(),
            DismissedAt = DateTime.UtcNow,
        });

        config["dismissedIssues"] = JsonSerializer.SerializeToElement(dismissed);
        integration.ConfigJson = JsonSerializer.Serialize(config);
        integration.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Issue dismissed", dismissed = dismissed.Count });
    }

    /// <summary>
    /// Removes a matching issue dismissal from the active integration configuration.
    /// </summary>
    [HttpPost("undismiss")]
    public async Task<IActionResult> UndismissIssue(
        Guid projectId, [FromBody] UndismissIssueRequest req, CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        if (!await IsActiveProjectMemberAsync(projectId, userId.Value, ct)) return Forbid();

        var integration = await _db.Integrations
            .FirstOrDefaultAsync(i => i.ProjectId == projectId && i.IsActive, ct);
        if (integration == null) return NotFound(new { error = "No active integration found" });

        var config = ParseConfig(integration.ConfigJson);
        var dismissed = GetDismissedFromConfig(config);

        dismissed.RemoveAll(d =>
            d.RuleId == req.RuleId && d.File == req.File && d.Line == req.Line);

        config["dismissedIssues"] = JsonSerializer.SerializeToElement(dismissed);
        integration.ConfigJson = JsonSerializer.Serialize(config);
        integration.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Issue re-opened", dismissed = dismissed.Count });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Parses an integration configuration JSON object, returning an empty dictionary when missing or invalid.
    /// </summary>
    private static Dictionary<string, JsonElement> ParseConfig(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// Returns whether the user is an active member of the project.
    /// </summary>
    /// <param name="projectId">Project whose membership is being checked.</param>
    /// <param name="userId">Authenticated user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when an active team membership row exists for the user and project.</returns>
    private Task<bool> IsActiveProjectMemberAsync(Guid projectId, Guid userId, CancellationToken ct) =>
        _db.TeamMembers.AnyAsync(
            tm => tm.ProjectId == projectId
                  && tm.UserId == userId
                  && tm.Status == TeamMemberStatus.Active.Value,
            ct);

    /// <summary>
    /// Reads a string-list property from parsed integration configuration.
    /// </summary>
    private static List<string> GetListFromConfig(Dictionary<string, JsonElement> config, string key)
    {
        if (!config.TryGetValue(key, out var el)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(el.GetRawText()) ?? []; }
        catch { return []; }
    }

    /// <summary>
    /// Reads dismissed issues from parsed integration configuration.
    /// </summary>
    private static List<DismissedIssueDto> GetDismissedFromConfig(Dictionary<string, JsonElement> config)
    {
        if (!config.TryGetValue("dismissedIssues", out var el)) return [];
        try { return JsonSerializer.Deserialize<List<DismissedIssueDto>>(el.GetRawText()) ?? []; }
        catch { return []; }
    }

    /// <summary>
    /// Maps a stored analysis result to the public response shape with deserialized JSON fields.
    /// </summary>
    private static object MapToResponse(CodeAnalysisResult r) => new
    {
        r.Id,
        r.ProjectId,
        r.IntegrationId,
        r.Repository,
        r.Branch,
        r.CommitSha,
        r.TotalIssues,
        r.TotalFiles,
        r.AnalysisTimeMs,
        SeverityCounts = r.SeverityCountsJson != null
            ? JsonSerializer.Deserialize<Dictionary<string, int>>(r.SeverityCountsJson)
            : null,
        CategoryCounts = r.CategoryCountsJson != null
            ? JsonSerializer.Deserialize<Dictionary<string, int>>(r.CategoryCountsJson)
            : null,
        Issues = r.IssuesJson != null
            ? JsonSerializer.Deserialize<List<JsonElement>>(r.IssuesJson)
            : null,
        r.Status,
        r.ErrorMessage,
        r.CreatedAt,
    };

    #endregion
}
