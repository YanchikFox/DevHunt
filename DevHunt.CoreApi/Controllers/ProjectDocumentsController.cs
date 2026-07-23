using System;
using System.ComponentModel.DataAnnotations;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Constants;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing project documentation (R11).
/// Extracted from ProjectsController to adhere to Single Responsibility Principle.
/// Routes: api/projects/{projectId}/docs/*
/// </summary>
[ApiController]
[Route("api/projects")]
public class ProjectDocumentsController : BaseProjectController
{
    private readonly IMLServiceClient _mlServiceClient;
    private readonly ILogger<ProjectDocumentsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectDocumentsController"/> class.
    /// </summary>
    /// <param name="dbContext">Database context used to read projects and persist documents.</param>
    /// <param name="auditService">Audit service that records document mutations.</param>
    /// <param name="notificationService">Notification service supplied to the base project controller.</param>
    /// <param name="eventBus">Event bus supplied to the base project controller.</param>
    /// <param name="cache">Cache service supplied to the base project controller.</param>
    /// <param name="mlServiceClient">Client that generates passport sections and Mermaid diagrams.</param>
    /// <param name="logger">Logger for AI document generation outcomes.</param>
    public ProjectDocumentsController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        ICacheService cache,
        IMLServiceClient mlServiceClient,
        ILogger<ProjectDocumentsController> logger)
        : base(dbContext, auditService, notificationService, eventBus, cache)
    {
        _mlServiceClient = mlServiceClient;
        _logger = logger;
    }

    /// <summary>Request to create a project document.</summary>
    /// <param name="Title">Document title.</param>
    /// <param name="Content">Document content.</param>
    /// <param name="DocumentType">Optional document type/category.</param>
    /// <param name="ContentFormat">Content format (markdown/html).</param>
    /// <param name="Path">Optional path/slug.</param>
    /// <param name="SortOrder">Sort order for listing.</param>
    /// <param name="IsPublic">Whether the document is public.</param>
    public record CreateDocumentRequest(
        [Required, MaxLength(200)] string Title,
        [Required] string Content,
        [MaxLength(50)] string? DocumentType,
        [MaxLength(20)] string ContentFormat = "markdown",
        [MaxLength(500)] string? Path = null,
        int SortOrder = 0,
        bool IsPublic = false);

    /// <summary>Request to update a project document.</summary>
    /// <param name="Title">Updated title.</param>
    /// <param name="Content">Updated content.</param>
    /// <param name="DocumentType">Updated document type.</param>
    /// <param name="ContentFormat">Updated content format.</param>
    /// <param name="Path">Updated path/slug.</param>
    /// <param name="SortOrder">Updated sort order.</param>
    /// <param name="IsPublic">Updated visibility flag.</param>
    public record UpdateDocumentRequest(
        string? Title,
        string? Content,
        [MaxLength(50)] string? DocumentType,
        [MaxLength(20)] string? ContentFormat,
        [MaxLength(500)] string? Path,
        int? SortOrder,
        bool? IsPublic);

    /// <summary>
    /// Gets paginated project documents, returning public documents to non-members and all non-deleted documents to project members.
    /// </summary>
    [HttpGet("{projectId:guid}/docs")]
    [Authorize]
    public async Task<IActionResult> GetProjectDocuments(
        Guid projectId,
        [FromQuery] string? documentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        // SECURITY: Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        // Check access
        var hasProjectAccess = await HasProjectAccessAsync(projectId, userId);

        var query = _dbContext.ProjectDocuments
            .AsNoTracking()
            .Include(pd => pd.Author)
            .Where(pd => pd.ProjectId == projectId && pd.DeletedAt == null);

        // Public documents visible to everyone, private - only to participants
        if (!hasProjectAccess)
        {
            query = query.Where(pd => pd.IsPublic);
        }

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            query = query.Where(pd => pd.DocumentType == documentType);
        }

        var total = await query.CountAsync(ct);

        var documents = await query
            .OrderBy(pd => pd.SortOrder)
            .ThenByDescending(pd => pd.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(pd => new
            {
                pd.Id,
                pd.Title,
                pd.DocumentType,
                pd.ContentFormat,
                pd.Path,
                pd.IsPublic,
                pd.ViewsCount,
                pd.CreatedAt,
                pd.UpdatedAt,
                Author = new
                {
                    pd.Author.Id,
                    pd.Author.FullName,
                    pd.Author.AvatarUrl
                },
                // Return only first 500 characters for list
                ContentPreview = pd.Content.Length > 500 ? pd.Content.Substring(0, 500) + "..." : pd.Content
            })
            .ToListAsync(ct);

        return Ok(new
        {
            Data = documents,
            Pagination = new
            {
                Page = page,
                PageSize = pageSize,
                Total = total,
                TotalPages = (int)Math.Ceiling((double)total / pageSize),
                HasNext = page * pageSize < total,
                HasPrevious = page > 1
            }
        });
    }

    /// <summary>
    /// Gets one project document, forbidding private-document access for non-members and incrementing the view counter on success.
    /// </summary>
    [HttpGet("{projectId:guid}/docs/{docId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetProjectDocument(Guid projectId, Guid docId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        var document = await _dbContext.ProjectDocuments
            .Include(pd => pd.Author)
            .FirstOrDefaultAsync(pd => pd.Id == docId && pd.ProjectId == projectId && pd.DeletedAt == null, ct);
        if (document == null) return NotFound("Document not found");

        // Check access
        var hasProjectAccess = await HasProjectAccessAsync(projectId, userId);

        if (!document.IsPublic && !hasProjectAccess)
            return Forbid("Document is private");

        // Increment views counter (DEV-117: atomic to avoid lost updates).
        await _dbContext.ProjectDocuments
            .Where(pd => pd.Id == docId)
            .ExecuteUpdateAsync(s => s.SetProperty(pd => pd.ViewsCount, pd => pd.ViewsCount + 1), ct);
        document.ViewsCount++; // reflect the increment in the response below

        return Ok(new
        {
            document.Id,
            document.Title,
            document.Content,
            document.ContentFormat,
            document.DocumentType,
            document.Path,
            document.IsPublic,
            document.ViewsCount,
            document.CreatedAt,
            document.UpdatedAt,
            Author = new
            {
                document.Author.Id,
                document.Author.FullName,
                document.Author.AvatarUrl
            }
        });
    }

    /// <summary>
    /// Creates a project document for active project members, sanitizing non-Mermaid content and returning a creation link.
    /// </summary>
    [ServiceFilter(typeof(ProfanityCensorFilter))]
    [HttpPost("{projectId:guid}/docs")]
    [Authorize]
    public async Task<IActionResult> CreateProjectDocument(Guid projectId, [FromBody] CreateDocumentRequest request, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        // Check access (team member or owner)
        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbid("Only project members can create documents");

        var document = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AuthorId = userId,
            Title = SecurityHelpers.SanitizeHtml(request.Title.Trim()),
            Content = IsMermaidFormat(request.ContentFormat)
                ? request.Content.Trim()
                : SecurityHelpers.SanitizeHtml(request.Content.Trim()),
            DocumentType = request.DocumentType,
            ContentFormat = request.ContentFormat ?? "markdown",
            Path = request.Path?.Trim(),
            SortOrder = request.SortOrder,
            IsPublic = request.IsPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.ProjectDocuments.Add(document);
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "ProjectDocumentsController.CreateProjectDocument", "ProjectDocument", document.Id,
            $"Created document: {request.Title}");

        return CreatedAtAction(nameof(GetProjectDocument), new { projectId, docId = document.Id }, new
        {
            document.Id,
            document.Title,
            document.CreatedAt
        });
    }

    /// <summary>
    /// Updates a non-deleted document when the caller is the author or project owner, preserving omitted fields.
    /// </summary>
    [ServiceFilter(typeof(ProfanityCensorFilter))]
    [HttpPut("{projectId:guid}/docs/{docId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateProjectDocument(Guid projectId, Guid docId, [FromBody] UpdateDocumentRequest request, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        var document = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(pd => pd.Id == docId && pd.ProjectId == projectId && pd.DeletedAt == null, ct);
        if (document == null) return NotFound("Document not found");

        // Only author or project owner can edit
        if (document.AuthorId != userId && document.Project.OwnerId != userId)
            return Forbid("Only document author or project owner can update document");

        if (!string.IsNullOrWhiteSpace(request.Title))
            document.Title = SecurityHelpers.SanitizeHtml(request.Title.Trim());
        if (!string.IsNullOrWhiteSpace(request.Content))
            document.Content = IsMermaidFormat(request.ContentFormat ?? document.ContentFormat)
                ? request.Content.Trim()
                : SecurityHelpers.SanitizeHtml(request.Content.Trim());
        if (request.DocumentType != null)
            document.DocumentType = request.DocumentType;
        if (request.ContentFormat != null)
            document.ContentFormat = request.ContentFormat;
        if (request.Path != null)
            document.Path = request.Path.Trim();
        if (request.SortOrder.HasValue)
            document.SortOrder = request.SortOrder.Value;
        if (request.IsPublic.HasValue)
            document.IsPublic = request.IsPublic.Value;

        document.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "ProjectDocumentsController.UpdateProjectDocument", "ProjectDocument", docId,
            $"Updated document: {document.Title}");

        return Ok(new
        {
            document.Id,
            document.Title,
            document.UpdatedAt
        });
    }

    /// <summary>
    /// Soft-deletes a document when the caller is the author or project owner.
    /// </summary>
    [HttpDelete("{projectId:guid}/docs/{docId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteProjectDocument(Guid projectId, Guid docId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        var document = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(pd => pd.Id == docId && pd.ProjectId == projectId && pd.DeletedAt == null, ct);
        if (document == null) return NotFound("Document not found");

        // Only author or project owner can delete
        if (document.AuthorId != userId && document.Project.OwnerId != userId)
            return Forbid("Only document author or project owner can delete document");

        // Soft delete
        document.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, "ProjectDocumentsController.DeleteProjectDocument", "ProjectDocument", docId,
            $"Deleted document: {document.Title}");

        return NoContent();
    }

    // ──────────────────────────────────────────────────────────────
    // AI Generation endpoints (replaces ProjectArtifactsController)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Request body for generating a Mermaid diagram document from project context.
    /// </summary>
    /// <param name="TechStack">Technology stack to use as diagram input.</param>
    /// <param name="Idea">Optional project idea or problem statement.</param>
    /// <param name="DiagramType">Requested diagram type, such as architecture or sequence.</param>
    /// <param name="ProjectContext">Optional caller-provided context appended before existing document excerpts.</param>
    public record GenerateDiagramRequest(
        [Required] string TechStack,
        string? Idea = null,
        string DiagramType = "architecture",
        string? ProjectContext = null);

    /// <summary>
    /// Generates or regenerates AI passport sections as project documents for active project members.
    /// Upserts AI documents by type — existing AI docs are updated, not duplicated.
    /// </summary>
    [HttpPost("{projectId:guid}/docs/generate-passport")]
    [Authorize]
    public async Task<IActionResult> GeneratePassport(
        Guid projectId,
        [FromHeader(Name = "Accept-Language")] string? locale,
        CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return NotFound("Project not found");

        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbid("Only active project members can generate passport");

        // Gather plan context
        var latestPlan = await _dbContext.AiPlans
            .AsNoTracking()
            .Where(p => p.ProjectId == projectId && p.Status == "applied")
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var phases = new List<AiPassportPhaseDto>();
        if (latestPlan?.PlanJson is not null)
            phases = ExtractPhasesFromPlanJson(latestPlan.PlanJson);

        // Gather existing documents as context for AI
        var documentsContext = await BuildDocumentsContextAsync(projectId, ct);

        try
        {
            string language = locale?.StartsWith("ru") == true ? "ru" : "en";
            var response = await _mlServiceClient.GeneratePassportAsync(
                idea: project.Description ?? project.Title,
                techStack: project.TechStack.Count > 0 ? string.Join(", ", project.TechStack) : "Not specified",
                description: project.Description,
                phases: phases,
                language: language,
                documentsContext: documentsContext);

            // Fetch existing AI documents for upsert
            var existingDocs = await _dbContext.ProjectDocuments
                .Where(d => d.ProjectId == projectId && d.DeletedAt == null
                    && (d.DocumentType == DocumentType.AiOverview
                     || d.DocumentType == DocumentType.AiTechStack
                     || d.DocumentType == DocumentType.AiArchitecture
                     || d.DocumentType == DocumentType.AiRoadmap
                     || d.DocumentType == DocumentType.AiDecisions))
                .ToListAsync(ct);

            foreach (var section in response.Sections)
            {
                string docType = DocumentType.FromArtifactType(section.Type);
                var existing = existingDocs.FirstOrDefault(d => d.DocumentType == docType);

                if (existing is not null)
                {
                    existing.Title = section.Title;
                    existing.Content = section.Content;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _dbContext.ProjectDocuments.Add(new ProjectDocument
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        AuthorId = userId,
                        Title = section.Title,
                        Content = section.Content,
                        DocumentType = docType,
                        ContentFormat = "markdown",
                        IsPublic = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
            }

            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Generated passport documents for project {ProjectId} with {SectionCount} sections",
                projectId, response.Sections.Count);

            // Return created/updated documents
            var savedDocs = await _dbContext.ProjectDocuments
                .AsNoTracking()
                .Where(d => d.ProjectId == projectId && d.DeletedAt == null
                    && DocumentType.IsAiGenerated(d.DocumentType)
                    && d.DocumentType != DocumentType.AiDiagram)
                .OrderBy(d => d.DocumentType)
                .Select(d => new
                {
                    d.Id, d.Title, d.Content, d.DocumentType,
                    d.ContentFormat, d.CreatedAt, d.UpdatedAt,
                })
                .ToListAsync(ct);

            return Ok(new
            {
                documents = savedDocs,
                provider = response.Provider,
                model = response.Model,
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "AI service unavailable" });
        }
    }

    /// <summary>
    /// Generates a Mermaid diagram for active project members and saves each request as a new AI diagram document.
    /// Each call creates a NEW document (diagrams are not overwritten).
    /// </summary>
    [HttpPost("{projectId:guid}/docs/generate-diagram")]
    [Authorize]
    public async Task<IActionResult> GenerateDiagram(
        Guid projectId,
        [FromBody] GenerateDiagramRequest request,
        CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return NotFound("Project not found");

        if (!await HasProjectAccessAsync(projectId, userId))
            return Forbid("Only active project members can generate diagrams");

        // Add existing documents as context
        var documentsContext = await BuildDocumentsContextAsync(projectId, ct);
        string? fullContext = string.IsNullOrEmpty(request.ProjectContext)
            ? documentsContext
            : $"{request.ProjectContext}\n\n{documentsContext}";

        try
        {
            var response = await _mlServiceClient.GenerateDiagramAsync(
                techStack: request.TechStack,
                idea: request.Idea,
                format: "mermaid",
                diagramType: request.DiagramType,
                projectContext: fullContext);

            var diagramLabels = new Dictionary<string, string>
            {
                ["architecture"] = "Architecture Diagram",
                ["sequence"] = "Sequence Diagram",
                ["erd"] = "ER Diagram",
                ["user_flow"] = "User Flow",
                ["deployment"] = "Deployment Diagram",
                ["state"] = "State Machine",
            };
            string title = diagramLabels.GetValueOrDefault(request.DiagramType, "Diagram");

            var document = new ProjectDocument
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                AuthorId = userId,
                Title = title,
                Content = response.Code,
                DocumentType = DocumentType.AiDiagram,
                ContentFormat = "mermaid",
                IsPublic = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _dbContext.ProjectDocuments.Add(document);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Generated {DiagramType} diagram document for project {ProjectId}",
                request.DiagramType, projectId);

            return Ok(new
            {
                document = new { document.Id, document.Title, document.Content,
                    document.DocumentType, document.ContentFormat, document.CreatedAt },
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "AI service unavailable" });
        }
    }

    /// <summary>
    /// Builds a compact context string from the ten most recently updated project documents for AI generation.
    /// </summary>
    private async Task<string?> BuildDocumentsContextAsync(Guid projectId, CancellationToken ct)
    {
        var docs = await _dbContext.ProjectDocuments
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId && d.DeletedAt == null)
            .OrderByDescending(d => d.UpdatedAt)
            .Take(10)
            .Select(d => new { d.Title, Preview = d.Content.Substring(0, Math.Min(d.Content.Length, 500)) })
            .ToListAsync(ct);

        if (docs.Count == 0) return null;

        var parts = docs.Select(d => $"### {d.Title}\n{d.Preview}");
        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Determines whether document content should be preserved as raw Mermaid instead of HTML-sanitized text.
    /// </summary>
    private static bool IsMermaidFormat(string? contentFormat)
        => string.Equals(contentFormat, "mermaid", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts phase names, descriptions, goals, and task counts from an applied AI plan JSON payload.
    /// </summary>
    private static List<AiPassportPhaseDto> ExtractPhasesFromPlanJson(string planJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(planJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("phases", out var phasesArray))
                return [];

            var phases = new List<AiPassportPhaseDto>();
            foreach (var phase in phasesArray.EnumerateArray())
            {
                string name = phase.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                string description = phase.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

                var goals = new List<string>();
                if (phase.TryGetProperty("goals", out var goalsArr))
                    foreach (var goal in goalsArr.EnumerateArray())
                    {
                        string? goalStr = goal.GetString();
                        if (goalStr is not null) goals.Add(goalStr);
                    }

                int taskCount = phase.TryGetProperty("tasks", out var tasksArr) ? tasksArr.GetArrayLength() : 0;
                phases.Add(new AiPassportPhaseDto(name, description, goals, taskCount));
            }

            return phases;
        }
        catch
        {
            return [];
        }
    }

}

