using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Categories the AI can record. Maps loosely to existing
/// <see cref="ProjectArtifact"/> section types; auto-trim happens at the
/// per-category level so a flood of one type doesn't push out the others.
/// </summary>
public enum ProjectMemoryCategory
{
    /// <summary>Meaningful product / architecture / process decision.</summary>
    Decision,
    /// <summary>AI-driven action: created task, moved column, etc.</summary>
    Action,
    /// <summary>Technical observation worth keeping (e.g. "EF jsonb requires X").</summary>
    Technical,
}

/// <summary>
/// Appends curated memory entries to project artifacts for long-term AI context.
/// </summary>
public interface IProjectMemoryService
{
    /// <summary>
    /// Appends a single memory entry to the project's curated artifact for
    /// the given category. The artifact is created on first call. The list
    /// is trimmed to the most recent N entries so it stays bounded; older
    /// entries are dropped from the artifact (they're still in audit logs
    /// if compliance ever needs them).
    /// </summary>
    Task RecordAsync(Guid projectId, Guid? userId, ProjectMemoryCategory category, string summary, CancellationToken ct);
}

/// <summary>
/// Persists curated long-term project memory into <see cref="ProjectArtifact"/> rows by category.
/// Used by the <c>record_decision</c> tool and invalidates project context cache on write.
/// </summary>
public sealed class ProjectMemoryService : IProjectMemoryService
{
    private const int MaxEntriesPerArtifact = 50;
    private const int MaxSummaryChars = 280;

    private readonly DevHuntDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<ProjectMemoryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectMemoryService"/> class.
    /// </summary>
    /// <param name="db">Database context used by this service.</param>
    /// <param name="cache">Cache used for project context and pending follow-up state.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    public ProjectMemoryService(DevHuntDbContext db, ICacheService cache, ILogger<ProjectMemoryService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordAsync(Guid projectId, Guid? userId, ProjectMemoryCategory category, string summary, CancellationToken ct)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(summary)) return;

        var trimmedSummary = SecurityHelpers.SanitizeHtml(summary.Trim());
        if (string.IsNullOrWhiteSpace(trimmedSummary)) return;

        if (trimmedSummary.Length > MaxSummaryChars)
        {
            trimmedSummary = trimmedSummary[..MaxSummaryChars] + "...";
        }

        var (artifactType, artifactTitle) = MapCategory(category);

        // B-08 compliant: single fetch + null check, no separate Any+First.
        var artifact = await _db.ProjectArtifacts
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Type == artifactType, ct);

        var now = DateTime.UtcNow;
        var newEntry = $"- {now:yyyy-MM-dd HH:mm} - {trimmedSummary}";

        if (artifact == null)
        {
            artifact = new ProjectArtifact
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Type = artifactType,
                Title = artifactTitle,
                Content = $"# {artifactTitle}\n\n{newEntry}",
                Version = 1,
                GeneratedAt = now,
                GeneratedByUserId = userId,
            };
            _db.ProjectArtifacts.Add(artifact);
        }
        else
        {
            artifact.Content = AppendAndTrim(artifact.Content, newEntry, artifactTitle);
            artifact.Version += 1;
            artifact.GeneratedAt = now;
            artifact.GeneratedByUserId = userId;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            // Invalidate the project-context cache; the next /ai turn must
            // see the new memory entry, otherwise the AI just wrote and
            // immediately won't remember it.
            await _cache.RemoveAsync($"ai:projctx:{projectId:N}");
        }
        catch (DbUpdateException ex)
        {
            // Concurrent writes can collide on the unique (ProjectId, Type)
            // index when two AI turns finish simultaneously. We don't retry
            // Losing one of the racing entries is preferable to a deadlock
            // chain, and the audit log keeps the full record either way.
            _logger.LogWarning(ex, "Concurrent ProjectMemory write collided for project {ProjectId} category {Category}", projectId, category);
        }
    }

    /// <summary>
    /// Appends a new line to the artifact's bullet list and trims to
    /// <see cref="MaxEntriesPerArtifact"/>. The artifact is conventionally
    /// "title heading + blank line + bullet list"; we treat any leading
    /// non-bullet lines as the header and only trim from the bullet section.
    /// </summary>
    private static string AppendAndTrim(string content, string newEntry, string fallbackTitle)
    {
        var lines = (content ?? string.Empty).Split('\n').ToList();
        var firstBulletIdx = lines.FindIndex(l => l.TrimStart().StartsWith("- "));
        List<string> header;
        List<string> bullets;
        if (firstBulletIdx < 0)
        {
            header = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (header.Count == 0) header.Add($"# {fallbackTitle}");
            bullets = new List<string>();
        }
        else
        {
            header = lines.Take(firstBulletIdx).ToList();
            bullets = lines.Skip(firstBulletIdx)
                .Where(l => l.TrimStart().StartsWith("- "))
                .ToList();
        }

        bullets.Add(newEntry);

        // Keep the most recent entries. Old ones drop from the top because
        // the artifact keeps chronological order top to bottom.
        if (bullets.Count > MaxEntriesPerArtifact)
        {
            bullets = bullets.Skip(bullets.Count - MaxEntriesPerArtifact).ToList();
        }

        var rebuilt = new List<string>(header);
        if (rebuilt.Count > 0 && !string.IsNullOrWhiteSpace(rebuilt[^1]))
        {
            rebuilt.Add(string.Empty);
        }
        rebuilt.AddRange(bullets);
        return string.Join('\n', rebuilt);
    }

    private static (string Type, string Title) MapCategory(ProjectMemoryCategory category) => category switch
    {
        ProjectMemoryCategory.Decision => ("decisions", "Project Decisions"),
        ProjectMemoryCategory.Action => ("ai_log", "AI Activity Log"),
        ProjectMemoryCategory.Technical => ("technical_notes", "Technical Notes"),
        _ => ("ai_log", "AI Activity Log"),
    };
}
