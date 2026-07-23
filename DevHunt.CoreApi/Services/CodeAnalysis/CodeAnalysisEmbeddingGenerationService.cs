using System.Text.Json;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.CodeAnalysis;

/// <summary>
/// Generates and persists pgvector embeddings for a stored code analysis result.
/// </summary>
public interface ICodeAnalysisEmbeddingGenerationService
{
    /// <summary>
    /// Builds embeddings from the analysis result issues JSON and stores them for semantic search.
    /// </summary>
    /// <param name="analysisResultId">Completed analysis result identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of rule groups embedded, or 0 when there are no issues.</returns>
    Task<int> GenerateForAnalysisResultAsync(Guid analysisResultId, CancellationToken ct);
}

/// <inheritdoc />
public sealed class CodeAnalysisEmbeddingGenerationService : ICodeAnalysisEmbeddingGenerationService
{
    private readonly DevHuntDbContext _db;
    private readonly IMLServiceClient _mlService;
    private readonly ILogger<CodeAnalysisEmbeddingGenerationService> _logger;

    /// <summary>
    /// Creates the embedding generation service.
    /// </summary>
    public CodeAnalysisEmbeddingGenerationService(
        DevHuntDbContext db,
        IMLServiceClient mlService,
        ILogger<CodeAnalysisEmbeddingGenerationService> logger)
    {
        _db = db;
        _mlService = mlService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> GenerateForAnalysisResultAsync(Guid analysisResultId, CancellationToken ct)
    {
        var result = await _db.CodeAnalysisResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == analysisResultId, ct);

        if (result?.IssuesJson == null)
        {
            return 0;
        }

        var parsed = JsonSerializer.Deserialize<List<JsonElement>>(result.IssuesJson) ?? [];
        if (parsed.Count == 0)
        {
            return 0;
        }

        var ruleGroups = BuildRuleGroups(parsed);
        if (ruleGroups.Count == 0)
        {
            return 0;
        }

        var groupList = ruleGroups.ToList();
        var texts = groupList.Select(g =>
        {
            var topFiles = g.Value.Files.GroupBy(x => x).OrderByDescending(x => x.Count()).Take(5).Select(x => x.Key);
            return $"{g.Value.RuleName ?? g.Key}: {g.Value.Message}. " +
                   $"Category: {g.Value.Category}. Severity: {g.Value.Severity}. " +
                   $"Found {g.Value.Count} times. " +
                   (g.Value.CweId != null ? $"CWE: {g.Value.CweId}. " : "") +
                   $"Files: {string.Join(", ", topFiles)}";
        }).ToList();

        var embeddings = await _mlService.GenerateEmbeddingsAsync(texts, ct);
        if (embeddings.Count == 0)
        {
            return 0;
        }

        await _db.Database.ExecuteSqlRawAsync(
            """DELETE FROM "CodeAnalysisEmbeddings" WHERE "ProjectId" = @p0""",
            [result.ProjectId],
            ct);

        for (var i = 0; i < groupList.Count && i < embeddings.Count; i++)
        {
            var g = groupList[i];
            var topFiles = g.Value.Files.GroupBy(x => x).OrderByDescending(x => x.Count()).Take(5).Select(x => x.Key).ToList();
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
                [
                    result.Id, result.ProjectId, g.Key, g.Value.RuleName ?? g.Key,
                    g.Value.Severity, g.Value.Category, g.Value.Count, texts[i],
                    g.Value.Message, topFilesJson, g.Value.CweId, g.Value.Suggestion,
                    vectorStr,
                ],
                ct);
        }

        _logger.LogInformation(
            "Generated {Count} embeddings for project {ProjectId} analysis {ResultId}",
            groupList.Count, result.ProjectId, result.Id);

        return groupList.Count;
    }

    private static Dictionary<string, RuleGroupState> BuildRuleGroups(List<JsonElement> parsed)
    {
        var ruleGroups = new Dictionary<string, RuleGroupState>();
        foreach (var issue in parsed)
        {
            var ruleId = issue.TryGetProperty("rule_id", out var rid) ? rid.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(ruleId))
            {
                continue;
            }

            var file = issue.TryGetProperty("file_path", out var fp) ? fp.GetString()
                     : issue.TryGetProperty("file", out var f) ? f.GetString() : null;

            if (ruleGroups.TryGetValue(ruleId, out var existing))
            {
                existing.Count++;
                if (file != null)
                {
                    existing.Files.Add(file);
                }
            }
            else
            {
                var files = new List<string>();
                if (file != null)
                {
                    files.Add(file);
                }

                ruleGroups[ruleId] = new RuleGroupState
                {
                    RuleName = issue.TryGetProperty("rule_name", out var rn) ? rn.GetString() : null,
                    Severity = issue.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "info" : "info",
                    Category = issue.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "",
                    Message = issue.TryGetProperty("message", out var msg) ? msg.GetString() : null,
                    CweId = issue.TryGetProperty("cwe_id", out var cwe) ? cwe.GetString() : null,
                    Suggestion = issue.TryGetProperty("suggestion", out var sug) ? sug.GetString() : null,
                    Count = 1,
                    Files = files,
                };
            }
        }

        return ruleGroups;
    }

    private sealed class RuleGroupState
    {
        public string? RuleName { get; init; }
        public string Severity { get; init; } = "info";
        public string Category { get; init; } = "";
        public string? Message { get; init; }
        public string? CweId { get; init; }
        public string? Suggestion { get; init; }
        public int Count { get; set; }
        public List<string> Files { get; init; } = [];
    }
}
