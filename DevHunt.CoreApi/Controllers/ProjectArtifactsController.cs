using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Legacy endpoints for reading, generating, and upserting AI project passport artifacts.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/artifacts")]
[Authorize]
[Obsolete("Use ProjectDocumentsController generate-passport/generate-diagram endpoints instead. Will be removed in a future release.")]
public class ProjectArtifactsController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly IMLServiceClient _mlServiceClient;
    private readonly ILogger<ProjectArtifactsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectArtifactsController"/> class.
    /// </summary>
    /// <param name="db">Database context used to read projects and persist generated artifacts.</param>
    /// <param name="mlServiceClient">Client that generates passport sections through the AI service.</param>
    /// <param name="logger">Logger for artifact generation outcomes.</param>
    public ProjectArtifactsController(
        DevHuntDbContext db,
        IMLServiceClient mlServiceClient,
        ILogger<ProjectArtifactsController> logger)
    {
        _db = db;
        _mlServiceClient = mlServiceClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets all passport artifacts for a project, requiring active membership when the project is private.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetArtifacts(Guid projectId, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return NotFound();

        // AUTH-02: Private projects require active membership to read artifacts
        if (project.Visibility == ProjectVisibility.Private.Value)
        {
            var userId = SecurityHelpers.GetUserId(User);
            bool isOwner = userId.HasValue && project.OwnerId == userId.Value;
            bool isMember = isOwner || (userId.HasValue && await _db.TeamMembers
                .AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == userId.Value
                             && tm.Status == TeamMemberStatus.Active.Value, ct));
            if (!isMember) return Forbid();
        }

        var artifacts = await _db.ProjectArtifacts
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.Type)
            .Select(a => new ArtifactDto(a.Id, a.Type, a.Title, a.Content, a.Version, a.GeneratedAt))
            .ToListAsync(ct);

        return Ok(new { artifacts });
    }

    /// <summary>
    /// Gets one artifact by type, requiring active membership when the project is private and returning not found for missing artifacts.
    /// </summary>
    [HttpGet("{type}")]
    public async Task<IActionResult> GetArtifactByType(Guid projectId, string type, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking()
            .Select(p => new { p.Id, p.Visibility, p.OwnerId })
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return NotFound();

        // AUTH-02: Private projects require active membership
        if (project.Visibility == ProjectVisibility.Private.Value)
        {
            var userId = SecurityHelpers.GetUserId(User);
            bool isOwner = userId.HasValue && project.OwnerId == userId.Value;
            bool isMember = isOwner || (userId.HasValue && await _db.TeamMembers
                .AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == userId.Value
                             && tm.Status == TeamMemberStatus.Active.Value, ct));
            if (!isMember) return Forbid();
        }

        var artifact = await _db.ProjectArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Type == type, ct);

        if (artifact is null) return NotFound();

        return Ok(new ArtifactDto(artifact.Id, artifact.Type, artifact.Title, artifact.Content, artifact.Version, artifact.GeneratedAt));
    }

    /// <summary>
    /// Generates or regenerates the full project passport for active members and upserts artifacts by section type.
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GeneratePassport(
        Guid projectId,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId is null) return Unauthorized();

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return NotFound();

        // AUTH-01: Require ACTIVE membership — ex-members (left/banned) must not regenerate artifacts
        bool isOwner = project.OwnerId == userId.Value;
        bool isMember = isOwner || await _db.TeamMembers
            .AnyAsync(tm => tm.ProjectId == projectId
                         && tm.UserId == userId.Value
                         && tm.Status == TeamMemberStatus.Active.Value, ct);
        if (!isMember) return Forbid();

        // Gather plan context if available
        var latestPlan = await _db.AiPlans
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId && p.Status == "applied")
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var phases = new List<AiPassportPhaseDto>();
        if (latestPlan?.PlanJson is not null)
        {
            phases = ExtractPhasesFromPlanJson(latestPlan.PlanJson);
        }

        try
        {
            string language = locale?.StartsWith("ru") == true ? "ru" : "en";
            AiPassportResponseDto response = await _mlServiceClient.GeneratePassportAsync(
                idea: project.Description ?? project.Title,
                techStack: project.TechStack.Count > 0 ? string.Join(", ", project.TechStack) : "Not specified",
                description: project.Description,
                phases: phases,
                language: language);

            // Upsert artifacts
            var existing = await _db.ProjectArtifacts
                .Where(a => a.ProjectId == projectId)
                .ToListAsync(ct);

            foreach (AiPassportSectionDto section in response.Sections)
            {
                ProjectArtifact? artifact = existing.FirstOrDefault(a => a.Type == section.Type);
                if (artifact is not null)
                {
                    artifact.Title = section.Title;
                    artifact.Content = section.Content;
                    artifact.Version += 1;
                    artifact.GeneratedAt = DateTime.UtcNow;
                    artifact.GeneratedByUserId = userId.Value;
                }
                else
                {
                    _db.ProjectArtifacts.Add(new ProjectArtifact
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        Type = section.Type,
                        Title = section.Title,
                        Content = section.Content,
                        Version = 1,
                        GeneratedAt = DateTime.UtcNow,
                        GeneratedByUserId = userId.Value,
                    });
                }
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Generated passport for project {ProjectId} with {SectionCount} sections",
                projectId,
                response.Sections.Count);

            // DATA-01: Return real saved IDs from DB instead of Guid.Empty
            var savedArtifacts = await _db.ProjectArtifacts
                .AsNoTracking()
                .Where(a => a.ProjectId == projectId)
                .Select(a => new ArtifactDto(a.Id, a.Type, a.Title, a.Content, a.Version, a.GeneratedAt))
                .ToListAsync(ct);

            return Ok(new
            {
                artifacts = savedArtifacts,
                provider = response.Provider,
                model = response.Model,
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "AI service unavailable" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Saves or updates a single artifact for active members, sanitizing content and incrementing the artifact version on edits.
    /// </summary>
    [HttpPut("{type}")]
    public async Task<IActionResult> UpsertArtifact(
        Guid projectId,
        string type,
        [FromBody] UpsertArtifactRequest request,
        CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (userId is null) return Unauthorized();

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return NotFound();

        // AUTH-01: Require ACTIVE membership — ex-members (left/banned) must not overwrite artifacts
        bool isOwner = project.OwnerId == userId.Value;
        bool isMember = isOwner || await _db.TeamMembers
            .AnyAsync(tm => tm.ProjectId == projectId
                         && tm.UserId == userId.Value
                         && tm.Status == TeamMemberStatus.Active.Value, ct);
        if (!isMember) return Forbid();

        ProjectArtifact? artifact = await _db.ProjectArtifacts
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Type == type, ct);

        if (artifact is not null)
        {
            artifact.Title = request.Title ?? artifact.Title;
            // XSS-01: Sanitize rich-text content before saving
            artifact.Content = SecurityHelpers.SanitizeHtml(request.Content);
            artifact.Version += 1;
            artifact.GeneratedAt = DateTime.UtcNow;
            artifact.GeneratedByUserId = userId.Value;
        }
        else
        {
            artifact = new ProjectArtifact
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Type = type,
                Title = request.Title ?? type,
                // XSS-01: Sanitize rich-text content before saving
                Content = SecurityHelpers.SanitizeHtml(request.Content),
                Version = 1,
                GeneratedAt = DateTime.UtcNow,
                GeneratedByUserId = userId.Value,
            };
            _db.ProjectArtifacts.Add(artifact);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new ArtifactDto(artifact.Id, artifact.Type, artifact.Title, artifact.Content, artifact.Version, artifact.GeneratedAt));
    }

    /// <summary>
    /// Extracts AI passport phase data from a stored plan JSON payload, returning an empty list for missing or invalid JSON.
    /// </summary>
    private static List<AiPassportPhaseDto> ExtractPhasesFromPlanJson(string planJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(planJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("phases", out var phasesArray))
                return new List<AiPassportPhaseDto>();

            var phases = new List<AiPassportPhaseDto>();
            foreach (var phase in phasesArray.EnumerateArray())
            {
                string name = phase.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                string description = phase.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

                var goals = new List<string>();
                if (phase.TryGetProperty("goals", out var goalsArr))
                {
                    foreach (var goal in goalsArr.EnumerateArray())
                    {
                        string? goalStr = goal.GetString();
                        if (goalStr is not null) goals.Add(goalStr);
                    }
                }

                int taskCount = 0;
                if (phase.TryGetProperty("tasks", out var tasksArr))
                {
                    taskCount = tasksArr.GetArrayLength();
                }

                phases.Add(new AiPassportPhaseDto(name, description, goals, taskCount));
            }

            return phases;
        }
        catch
        {
            return new List<AiPassportPhaseDto>();
        }
    }
}

/// <summary>
/// Artifact payload returned to clients after reading or saving generated project content.
/// </summary>
/// <param name="Id">Artifact identifier.</param>
/// <param name="Type">Artifact section type, such as overview or architecture.</param>
/// <param name="Title">Human-readable artifact title.</param>
/// <param name="Content">Stored artifact content.</param>
/// <param name="Version">Artifact revision number.</param>
/// <param name="GeneratedAt">UTC timestamp of the latest generation or upsert.</param>
public record ArtifactDto(
    Guid Id,
    string Type,
    string? Title,
    string Content,
    int Version,
    DateTime GeneratedAt
);

/// <summary>
/// Request body for creating or replacing one artifact section.
/// </summary>
/// <param name="Content">Artifact body to sanitize and persist.</param>
/// <param name="Title">Optional display title; existing titles are kept when omitted during updates.</param>
public record UpsertArtifactRequest(
    string Content,
    string? Title = null
);
