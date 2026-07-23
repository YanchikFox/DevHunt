using System.Text;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskStatus = DevHunt.CoreApi.Models.TaskStatus;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Builds markdown project context blocks for LLM system prompts.
/// </summary>
public interface IProjectContextBuilder
{
    /// <summary>
    /// Builds a compact markdown system-prompt block describing the project
    /// behind a conversation. Returns an empty string for non-project chats
    /// (DMs, ad-hoc groups). Cached per project for a short window so
    /// back-to-back /ai turns don't keep hitting the DB.
    /// </summary>
    Task<string> BuildAsync(Guid projectId, CancellationToken ct);
}

/// <summary>
/// Builds a cached markdown project context block appended to LLM system prompts for project conversations.
/// </summary>
public sealed class ProjectContextBuilder : IProjectContextBuilder
{
    // Token-budget targets (1 token is roughly 4 chars). The prompt only needs
    // enough project shape to answer well; full documents stay on-demand via
    // read_document, and task mutation tools can fetch exact entities later.
    private const int MaxActiveTasks = 8;
    private const int MaxArtifacts = 4;
    private const int ArtifactPreviewChars = 360;
    private const int MaxDocuments = 8;
    private const int MaxAiLogEntries = 8;
    private const int CacheTtlSeconds = 300;

    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<ProjectContextBuilder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectContextBuilder"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="cache">Cache used for project context and pending follow-up state.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public ProjectContextBuilder(DevHuntDbContext db, ICacheService cache, ILogger<ProjectContextBuilder> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> BuildAsync(Guid projectId, CancellationToken ct)
    {
        if (projectId == Guid.Empty) return string.Empty;

        var cacheKey = $"ai:projctx:{projectId:N}";
        try
        {
            var cached = await _cache.GetAsync<CachedContext>(cacheKey);
            if (cached != null) return cached.Value;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Project context cache read failed for {ProjectId}; rebuilding", projectId);
        }

        var built = await BuildFreshAsync(projectId, ct);

        try
        {
            await _cache.SetAsync(cacheKey, new CachedContext(built), TimeSpan.FromSeconds(CacheTtlSeconds));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Project context cache write failed for {ProjectId}", projectId);
        }

        return built;
    }

    /// <summary>Loads project, tasks, artifacts, and documents from the database without cache.</summary>
    private async Task<string> BuildFreshAsync(Guid projectId, CancellationToken ct)
    {
        // One trip per piece. Kept separate so EF can stream them in parallel
        // through the same DbContext (we await sequentially because DbContext
        // isn't thread-safe; profile shows these together are <50ms).
        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new ProjectSummary(
                p.Title,
                p.ShortDescription ?? p.Description,
                p.TechStack,
                p.Status,
                p.DifficultyLevel))
            .FirstOrDefaultAsync(ct);

        if (project == null) return string.Empty;

        var memberCount = await _db.TeamMembers
            .CountAsync(tm => tm.ProjectId == projectId
                && tm.Status == TeamMemberStatus.Active.Value, ct);

        var activeTasks = await _db.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && !t.IsDeleted
                && t.Status != TaskStatus.Done.Value
                && t.Status != TaskStatus.Archived.Value
                && t.Status != TaskStatus.Cancelled.Value)
            .OrderByDescending(t => t.Priority == TaskPriority.Urgent.Value ? 2
                : t.Priority == TaskPriority.High.Value ? 1
                : 0)
            .ThenByDescending(t => t.CreatedAt)
            .Take(MaxActiveTasks)
            .Select(t => new TaskSummary(t.Title, t.Status, t.Priority))
            .ToListAsync(ct);

        var artifacts = await _db.ProjectArtifacts
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.Type)
            .Take(MaxArtifacts)
            .Select(a => new { a.Type, a.Title, a.Content })
            .ToListAsync(ct);

        var documents = await _db.ProjectDocuments
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && d.DeletedAt == null)
            .OrderBy(d => d.SortOrder)
            .ThenByDescending(d => d.UpdatedAt)
            .Take(MaxDocuments)
            .Select(d => new { d.Id, d.Title, d.DocumentType })
            .ToListAsync(ct);

        return Render(project, memberCount, activeTasks, artifacts.Select(a =>
                new ArtifactSummary(a.Type, a.Title, Truncate(a.Content, ArtifactPreviewChars))).ToList(),
            documents.Select(d => new DocumentSummary(d.Id, d.Title, d.DocumentType)).ToList());
    }

    /// <summary>Formats loaded summaries into the markdown block injected into the system prompt.</summary>
    private static string Render(
        ProjectSummary project,
        int memberCount,
        IReadOnlyList<TaskSummary> tasks,
        IReadOnlyList<ArtifactSummary> artifacts,
        IReadOnlyList<DocumentSummary> docs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Project context");
        sb.Append("Name: ").Append(project.Title);
        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            sb.Append(" - ").Append(Truncate(project.Description, 240));
        }
        sb.AppendLine();
        sb.Append("Status: ").Append(project.Status);
        sb.Append(" | Difficulty: ").Append(string.IsNullOrWhiteSpace(project.DifficultyLevel) ? "n/a" : project.DifficultyLevel);
        sb.Append(" | Active members: ").Append(memberCount).AppendLine();
        if (project.TechStack is { Count: > 0 })
        {
            sb.Append("Stack: ").AppendLine(string.Join(", ", project.TechStack));
        }

        // Artifacts come first because they encode the curated team memory
        // (decisions, architecture). LLM should read these before reasoning
        // about anything else.
        if (artifacts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Project memory (artifacts)");
            foreach (var a in artifacts)
            {
                var heading = string.IsNullOrWhiteSpace(a.Title) ? a.Type : a.Title;
                sb.Append("### ").Append(heading).Append(" (`").Append(a.Type).AppendLine("`)");
                sb.AppendLine(a.Preview);
                sb.AppendLine();
            }
        }

        if (tasks.Count > 0)
        {
            sb.AppendLine($"## Active tasks ({tasks.Count})");
            foreach (var t in tasks)
            {
                sb.Append("- [").Append(t.Priority ?? "med").Append("] ").Append(t.Title)
                    .Append(" | ").AppendLine(t.Status);
            }
            sb.AppendLine();
        }

        if (docs.Count > 0)
        {
            sb.AppendLine("## Available documents (use read_document to fetch)");
            foreach (var d in docs)
            {
                sb.Append("- ").Append(d.Title).Append(" (id: ").Append(d.Id);
                if (!string.IsNullOrWhiteSpace(d.DocumentType))
                {
                    sb.Append(", type: ").Append(d.DocumentType);
                }
                sb.AppendLine(")");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Truncates long text fields to stay within token budget targets.</summary>
    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max] + "...";
    }

    /// <summary>
    /// Stores the rendered project context string in the short-lived cache.
    /// </summary>
    private sealed record CachedContext(string Value);

    /// <summary>
    /// Carries project-level details used to render the LLM context header.
    /// </summary>
    private sealed record ProjectSummary(string Title, string Description, List<string> TechStack, string Status, string? DifficultyLevel);

    /// <summary>
    /// Carries active task details used to render the task list in project context.
    /// </summary>
    private sealed record TaskSummary(string Title, string Status, string? Priority);

    /// <summary>
    /// Carries artifact details and a bounded preview for project memory context.
    /// </summary>
    private sealed record ArtifactSummary(string Type, string? Title, string Preview);

    /// <summary>
    /// Carries document metadata exposed to the model for follow-up document reads.
    /// </summary>
    private sealed record DocumentSummary(Guid Id, string Title, string? DocumentType);
}
