using System.ComponentModel.DataAnnotations;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Badges;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.CoreApi.Models;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Controller for managing projects (Core API) - REFACTORED (R13).
/// Implements core CRUD operations according to SRS:
/// - Project creation and editing (UC-2)
/// - Project search with filtering (UC-3)
///
/// Functionality moved to specialized controllers:
/// - ProjectLifecycleController: archive, unarchive, publish, activate, complete, cancel
/// - ProjectMetricsController: metrics, velocity, contributions
/// - ProjectFilesController: file sharing (api/projects/{id}/files/*)
/// - ShowcaseController: showcase publication (api/showcase/*)
/// - ProjectDocumentsController: documentation (api/projects/{id}/docs/*)
/// - ProjectIssuesController: issues (api/projects/{id}/issues/*)
/// - ProjectSubscriptionsController: subscriptions (MOVED - R13)
///
/// REFACTORED (R13):
/// - Removed base class inheritance (Constructor Over-Injection fixed)
/// - Moved subscriptions to ProjectSubscriptionsController (Low Cohesion fixed)
/// - Added value objects ProjectStatus and ProjectVisibility (Primitive Obsession fixed)
/// </summary>
[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly DevHuntDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IEventBusService _eventBus;
    private readonly ICacheService _cache;
    private readonly IProjectServices _projectServices;



    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectsController"/> class.
    /// </summary>
    /// <param name="dbContext">Database context used for project CRUD and related entity queries.</param>
    /// <param name="auditService">Audit service that records high-risk project changes.</param>
    /// <param name="eventBus">Event bus used to publish project lifecycle and ownership events.</param>
    /// <param name="cache">Cache service used to invalidate project detail and list entries.</param>
    /// <param name="projectServices">Service facade for project filtering, permissions, and activity logging.</param>
    public ProjectsController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        IEventBusService eventBus,
        ICacheService cache,
        IProjectServices projectServices)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _eventBus = eventBus;
        _cache = cache;
        _projectServices = projectServices;
    }

    #region DTOs

    /// <summary>
    /// Summary DTO for project list views. Contains basic info for cards/tables.
    /// </summary>
    /// <param name="Id">Unique project identifier.</param>
    /// <param name="Title">Project title.</param>
    /// <param name="ShortDescription">Brief description for cards.</param>
    /// <param name="Description">Full project description.</param>
    /// <param name="TechStack">Technologies used (e.g., ["React", "Node.js"]).</param>
    /// <param name="Status">Current status: draft, recruiting, active, completed, archived, cancelled.</param>
    /// <param name="Visibility">Access level: public, private, unlisted, members, subscribers.</param>
    /// <param name="OwnerId">User ID of project owner.</param>
    /// <param name="DifficultyLevel">Project complexity: beginner, intermediate, advanced.</param>
    /// <param name="ExpectedDurationDays">Estimated project duration in days.</param>
    /// <param name="ShowcasePublished">Whether project is visible in public showcase.</param>
    /// <param name="Featured">Whether project is featured on homepage.</param>
    /// <param name="TeamSize">Current number of active team members.</param>
    /// <param name="MaxTeamSize">Maximum allowed team size (null = unlimited).</param>
    /// <param name="Rating">Average rating (1-5, null if no ratings).</param>
    /// <param name="CreatedAt">Creation timestamp (UTC).</param>
    /// <param name="UpdatedAt">Last update timestamp (UTC).</param>
    /// <param name="Slug">Optional URL-safe project handle.</param>
    /// <param name="BoostsCount">Denormalized boost count.</param>
    /// <param name="OpenRolesCount">Calculated number of unfilled role seats.</param>
    /// <param name="BoostedByMe">Whether the current viewer has boosted the project.</param>
    public record ProjectSummaryDto(
        Guid Id, string Title, string? ShortDescription, string Description,
        IReadOnlyCollection<string> TechStack, string Status, string Visibility,
        Guid OwnerId, string? DifficultyLevel, int? ExpectedDurationDays,
        bool ShowcasePublished, bool Featured, int TeamSize, int? MaxTeamSize,
        float? Rating, DateTime CreatedAt, DateTime UpdatedAt,
        string? Slug, int BoostsCount, int OpenRolesCount, bool BoostedByMe);

    /// <summary>
    /// Structured open role requirement exposed in project details.
    /// </summary>
    /// <param name="Role">Role label being recruited for.</param>
    /// <param name="TotalNeeded">Number of people needed for the role.</param>
    /// <param name="HoursPerWeek">Optional expected weekly time commitment.</param>
    /// <param name="EquityOptional">Whether equity is optional for the role.</param>
    public record OpenRoleDto(string Role, int TotalNeeded, int? HoursPerWeek, bool EquityOptional);

    /// <summary>
    /// Detailed project payload returned for single-project views.
    /// </summary>
    /// <param name="Id">Project identifier.</param>
    /// <param name="Title">Project title.</param>
    /// <param name="ShortDescription">Brief project summary for compact displays.</param>
    /// <param name="Description">Full project description.</param>
    /// <param name="TechStack">Technologies associated with the project.</param>
    /// <param name="Status">Current lifecycle status.</param>
    /// <param name="Visibility">Project access level.</param>
    /// <param name="DefaultNewsVisibility">Default visibility applied to new news posts.</param>
    /// <param name="DefaultFilesVisibility">Default visibility applied to new uploaded files.</param>
    /// <param name="OwnerId">User identifier of the project owner.</param>
    /// <param name="DifficultyLevel">Optional difficulty label.</param>
    /// <param name="ExpectedDurationDays">Optional expected duration in days.</param>
    /// <param name="ShowcasePublished">Whether the project is published to the showcase.</param>
    /// <param name="Featured">Whether the project is featured.</param>
    /// <param name="MaxTeamSize">Maximum team size, or null when unlimited.</param>
    /// <param name="Rating">Average project rating when reviews exist.</param>
    /// <param name="CreatedAt">UTC creation timestamp.</param>
    /// <param name="UpdatedAt">UTC last update timestamp.</param>
    /// <param name="RequiredRoles">Legacy role labels requested for the project.</param>
    /// <param name="StartDate">Optional project start date.</param>
    /// <param name="EndDate">Optional project end date.</param>
    /// <param name="Team">Active and historical team member summaries included for authorized viewers.</param>
    /// <param name="Tasks">Task summaries included for authorized viewers.</param>
    /// <param name="Invitations">Pending and historical invitation summaries included for authorized viewers.</param>
    /// <param name="OpenRoles">Structured open role requirements.</param>
    /// <param name="Slug">Optional URL-safe project handle.</param>
    /// <param name="BoostsCount">Denormalized boost count.</param>
    /// <param name="OpenRolesCount">Calculated number of unfilled role seats.</param>
    /// <param name="BoostedByMe">Whether the current viewer has boosted the project.</param>
    public record ProjectDetailsDto(
        Guid Id, string Title, string? ShortDescription, string Description,
        IReadOnlyCollection<string> TechStack, string Status, string Visibility,
        string DefaultNewsVisibility, string DefaultFilesVisibility, Guid OwnerId,
        string? DifficultyLevel, int? ExpectedDurationDays, bool ShowcasePublished,
        bool Featured, int? MaxTeamSize, float? Rating, DateTime CreatedAt, DateTime UpdatedAt,
        IReadOnlyCollection<string> RequiredRoles, DateTime? StartDate, DateTime? EndDate,
        IReadOnlyCollection<ProjectTeamMemberDto> Team,
        IReadOnlyCollection<ProjectTaskDto> Tasks,
        IReadOnlyCollection<ProjectInvitationDto> Invitations,
        IReadOnlyCollection<OpenRoleDto>? OpenRoles,
        string? Slug, int BoostsCount, int OpenRolesCount, bool BoostedByMe);

    /// <summary>Team member info for project details.</summary>
    /// <param name="Id">Team membership identifier.</param>
    /// <param name="UserId">User identifier for authorized viewers; empty for sanitized public responses.</param>
    /// <param name="Email">User email for authorized viewers; empty for sanitized public responses.</param>
    /// <param name="FullName">User display name for authorized viewers; null for sanitized public responses.</param>
    /// <param name="Role">Member role in the project.</param>
    /// <param name="Contribution">Optional contribution description.</param>
    /// <param name="JoinedAt">UTC timestamp when the member joined.</param>
    public record ProjectTeamMemberDto(Guid Id, Guid UserId, string Email, string? FullName, string Role, string? Contribution, DateTime JoinedAt);

    /// <summary>Task summary for project details.</summary>
    /// <param name="Id">Task identifier.</param>
    /// <param name="Title">Task title.</param>
    /// <param name="Status">Task workflow status.</param>
    /// <param name="AssignedToUserId">Assigned user identifier, if any.</param>
    /// <param name="Deadline">Optional task deadline.</param>
    /// <param name="Priority">Task priority label.</param>
    /// <param name="Tags">Optional task tags.</param>
    public record ProjectTaskDto(Guid Id, string Title, string Status, Guid? AssignedToUserId, DateTime? Deadline, string? Priority = "medium", string? Tags = null);

    /// <summary>Pending invitation info for project details.</summary>
    /// <param name="Id">Invitation identifier.</param>
    /// <param name="InviteeId">Invited user identifier.</param>
    /// <param name="Email">Invitee email.</param>
    /// <param name="FullName">Invitee display name.</param>
    /// <param name="Role">Proposed role for the invitee.</param>
    /// <param name="Status">Invitation status.</param>
    /// <param name="CreatedAt">UTC timestamp when the invitation was created.</param>
    /// <param name="RespondedAt">UTC timestamp when the invitation was answered, if any.</param>
    public record ProjectInvitationDto(Guid Id, Guid InviteeId, string Email, string? FullName, string Role, string Status, DateTime CreatedAt, DateTime? RespondedAt);

    /// <summary>
    /// Structured role requirement supplied while creating or updating a project.
    /// </summary>
    /// <param name="Role">Role label to recruit for.</param>
    /// <param name="TotalNeeded">Optional number of people needed; defaults to one when persisted.</param>
    /// <param name="HoursPerWeek">Optional expected weekly time commitment.</param>
    /// <param name="EquityOptional">Whether equity is optional for the role.</param>
    public record OpenRoleRequest(string Role, int? TotalNeeded, int? HoursPerWeek, bool EquityOptional);

    /// <summary>
    /// Request body for creating a project and its owner team membership.
    /// </summary>
    /// <param name="Title">Required project title.</param>
    /// <param name="ShortDescription">Optional compact project summary.</param>
    /// <param name="Description">Project description to sanitize and persist.</param>
    /// <param name="TechStack">Optional technology labels to normalize.</param>
    /// <param name="Status">Initial status; only draft or recruiting are accepted.</param>
    /// <param name="Visibility">Project visibility, defaulting to public.</param>
    /// <param name="DefaultNewsVisibility">Default visibility for new news posts.</param>
    /// <param name="DefaultFilesVisibility">Default visibility for uploaded files.</param>
    /// <param name="DifficultyLevel">Optional difficulty label.</param>
    /// <param name="ExpectedDurationDays">Optional expected duration in days.</param>
    /// <param name="ShowcasePublished">Requested showcase publication flag; honored only for admins/curators.</param>
    /// <param name="Featured">Requested featured flag; honored only for admins/curators.</param>
    /// <param name="MaxTeamSize">Optional maximum team size.</param>
    /// <param name="RequiredRoles">Legacy role labels requested for the project.</param>
    /// <param name="StartDate">Optional project start date.</param>
    /// <param name="EndDate">Optional project end date.</param>
    /// <param name="OpenRoles">Structured open role requirements.</param>
    /// <param name="Slug">Optional URL-safe project handle.</param>
    public record CreateProjectRequest(
        string Title, string? ShortDescription, string Description,
        List<string>? TechStack, string? Status, string? Visibility,
        string? DefaultNewsVisibility, string? DefaultFilesVisibility,
        string? DifficultyLevel, int? ExpectedDurationDays, bool ShowcasePublished,
        bool Featured, int? MaxTeamSize, List<string>? RequiredRoles,
        DateTime? StartDate, DateTime? EndDate, List<OpenRoleRequest>? OpenRoles,
        string? Slug = null);

    // PE-08: ShowcasePublished and Featured are nullable — null means "don't change"
    /// <summary>
    /// Project update payload where nullable fields may preserve existing values.
    /// </summary>
    /// <param name="Title">Replacement project title when provided.</param>
    /// <param name="ShortDescription">Replacement compact summary when provided.</param>
    /// <param name="Description">Replacement description to sanitize when provided.</param>
    /// <param name="TechStack">Replacement technology labels when provided.</param>
    /// <param name="Status">Replacement status string when provided and recognized.</param>
    /// <param name="Visibility">Replacement project visibility when provided.</param>
    /// <param name="DefaultNewsVisibility">Replacement default news visibility when provided.</param>
    /// <param name="DefaultFilesVisibility">Replacement default file visibility when provided.</param>
    /// <param name="DifficultyLevel">Replacement difficulty label when provided.</param>
    /// <param name="ExpectedDurationDays">Replacement expected duration when provided.</param>
    /// <param name="ShowcasePublished">Replacement showcase publication flag when explicitly provided.</param>
    /// <param name="Featured">Replacement featured flag when explicitly provided by an admin/curator.</param>
    /// <param name="MaxTeamSize">Replacement maximum team size when provided.</param>
    /// <param name="RequiredRoles">Replacement legacy role labels when provided.</param>
    /// <param name="StartDate">Replacement project start date when provided.</param>
    /// <param name="EndDate">Replacement project end date when provided.</param>
    /// <param name="OpenRoles">Replacement structured open roles when provided.</param>
    /// <param name="Slug">Replacement slug; an empty string clears the slug.</param>
    public record UpdateProjectRequest(
        string Title, string? ShortDescription, string Description,
        List<string>? TechStack, string? Status, string? Visibility,
        string? DefaultNewsVisibility, string? DefaultFilesVisibility,
        string? DifficultyLevel, int? ExpectedDurationDays, bool? ShowcasePublished,
        bool? Featured, int? MaxTeamSize, List<string>? RequiredRoles,
        DateTime? StartDate, DateTime? EndDate, List<OpenRoleRequest>? OpenRoles,
        string? Slug = null);

    /// <summary>Request to change project visibility.</summary>
    /// <param name="Visibility">New visibility: public, private, unlisted, members, or subscribers.</param>
    public record UpdateVisibilityRequest(string Visibility);

    /// <summary>Request to update project settings.</summary>
    public record UpdateProjectSettingsRequest(string? Visibility, string? DefaultNewsVisibility, string? DefaultFilesVisibility);

    /// <summary>Request to change project status.</summary>
    /// <param name="Status">New status: draft, recruiting, active, completed, archived, or cancelled.</param>
    public record ChangeStatusRequest([Required][RegularExpression("^(draft|recruiting|active|completed|archived|cancelled)$")] string Status);

    /// <summary>Request to transfer project ownership.</summary>
    /// <param name="NewOwnerId">User ID of the new owner (must be a team member).</param>
    /// <param name="Reason">Optional. Reason for ownership transfer.</param>
    public record TransferOwnershipRequest(Guid NewOwnerId, string? Reason);

    /// <summary>
    /// Parameter object for project visibility checks (reduces argument count).
    /// </summary>
    private record ProjectViewContext(Guid OwnerId, string Visibility, string Status, Guid ProjectId, IReadOnlySet<Guid> ActiveMemberUserIds)
    {
        /// <summary>
        /// Builds a visibility-check context from a tracked or loaded project entity.
        /// </summary>
        /// <param name="p">Project entity with team members loaded.</param>
        /// <returns>Project view context containing owner, visibility, status, project id, and active member IDs.</returns>
        public static ProjectViewContext FromProject(Project p) => new(p.OwnerId, p.Visibility, p.Status, p.Id,
            p.TeamMembers.Where(tm => tm.Status == TeamMemberStatus.Active).Select(tm => tm.UserId).ToHashSet());
        /// <summary>
        /// Builds a visibility-check context from a cached project details DTO.
        /// </summary>
        /// <param name="dto">Cached project details DTO.</param>
        /// <param name="projectId">Project identifier that scopes the cached DTO.</param>
        /// <returns>Project view context containing owner, visibility, status, project id, and active member IDs.</returns>
        public static ProjectViewContext FromDto(ProjectDetailsDto dto, Guid projectId) => new(dto.OwnerId, dto.Visibility, dto.Status, projectId,
            dto.Team.Select(tm => tm.UserId).ToHashSet());
    }

    #endregion

    #region List & Get

    /// <summary>
    /// Lists projects with filtering, sorting, pagination, and viewer-aware "my projects" enforcement.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetProjects([FromQuery] ProjectQueryParams queryParams, CancellationToken ct = default)
    {
        var requesterId = SecurityHelpers.GetUserId(User);
        var isPrivilegedViewer = SecurityHelpers.IsAdminOrCurator(User);

        if (queryParams.MyProjects == true && !requesterId.HasValue)
            return Forbid("Authentication required to filter by your own projects");

        var options = ProjectFilterOptions.FromQuery(
            requesterId, isPrivilegedViewer, queryParams.Status, queryParams.Visibility,
            queryParams.Query, queryParams.Tech, queryParams.Difficulty, queryParams.Showcase,
            queryParams.Featured, queryParams.MyProjects, queryParams.OwnerId, queryParams.ExcludeDrafts,
            queryParams.IncludeCancelled, queryParams.OwnerTimezone, queryParams.Page,
            queryParams.PageSize, queryParams.SortBy, queryParams.SortOrder);

        // Build base query
        var projectsQuery = _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.TeamMembers)
            .Include(p => p.Tasks)
            .AsSplitQuery()
            .AsQueryable();

        // Apply filters using service
        projectsQuery = await _projectServices.Filter.ApplyFiltersAsync(projectsQuery, options);

        // Apply sorting
        projectsQuery = _projectServices.Filter.ApplySorting(projectsQuery, options.SortBy, options.SortOrder);

        // Get total count
        var total = await projectsQuery.CountAsync(ct);

        // Apply pagination — fetch domain entities (keeps OpenRoles, Boosts in scope for DTO mapping).
        var page = await projectsQuery
            .Include(p => p.Boosts)
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToListAsync(ct);

        var results = page
            .Select(p => MapToSummaryDto(p, boostedByMe: requesterId.HasValue && p.Boosts.Any(b => b.UserId == requesterId.Value)))
            .ToList();

        return Ok(new
        {
            Data = results,
            Pagination = new
            {
                Page = options.Page,
                PageSize = options.PageSize,
                Total = total,
                TotalPages = (int)Math.Ceiling((double)total / options.PageSize),
                HasNext = options.Page * options.PageSize < total,
                HasPrevious = options.Page > 1
            }
        });
    }

    /// <summary>
    /// Gets project details from cache or database, enforcing draft and restricted-visibility rules before returning sanitized public data.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDetailsDto>> GetProject(Guid id, CancellationToken ct = default)
    {
        var viewerId = SecurityHelpers.GetUserId(User);
        var isPrivilegedViewer = SecurityHelpers.IsAdminOrCurator(User);

        // Try cache first (cached DTO is viewer-agnostic; boost state is resolved per-request below).
        var cacheKey = $"project:{id}";
        var cached = await _cache.GetAsync<ProjectDetailsDto>(cacheKey);
        if (cached != null)
        {
            var accessResult = CheckProjectAccess(ProjectViewContext.FromDto(cached, id), viewerId, isPrivilegedViewer);
            if (accessResult.Action != null) return accessResult.Action;

            var cachedBoostedByMe = viewerId.HasValue &&
                await _dbContext.ProjectBoosts.AsNoTracking()
                    .AnyAsync(b => b.ProjectId == id && b.UserId == viewerId.Value, ct);
            var cachedWithViewer = cached with { BoostedByMe = cachedBoostedByMe };
            return accessResult.FullAccess ? cachedWithViewer : SanitizeProjectForPublicView(cachedWithViewer);
        }

        var p = await _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.TeamMembers).ThenInclude(tm => tm.User)
            .Include(p => p.Tasks)
            .Include(p => p.Invitations).ThenInclude(i => i.Invitee)
            .Include(p => p.Boosts)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (p is null) return NotFound();

        var access = CheckProjectAccess(ProjectViewContext.FromProject(p), viewerId, isPrivilegedViewer);
        if (access.Action != null) return access.Action;

        var boostedByMe = viewerId.HasValue && p.Boosts.Any(b => b.UserId == viewerId.Value);
        var dto = MapToDetailsDto(p, boostedByMe);
        await _cache.SetAsync(cacheKey, dto with { BoostedByMe = false }, TimeSpan.FromMinutes(15));

        return access.FullAccess ? dto : SanitizeProjectForPublicView(dto);
    }

    /// <summary>
    /// Gets the authenticated user's project permissions, returning not found when the project permission context is missing.
    /// </summary>
    [HttpGet("{id:guid}/permissions")]
    [Authorize]
    public async Task<IActionResult> GetProjectPermissions(Guid id)
    {
        var userId = GetRequiredUserId();
        var isAdmin = SecurityHelpers.IsAdminOrCurator(User);

        var permissions = await _projectServices.Permissions.GetPermissionsAsync(id, userId, isAdmin);
        if (permissions == null) return NotFound();

        return Ok(permissions);
    }

    /// <summary>
    /// GET /api/projects/by-slug/{slug} - Look up a project by its URL-safe handle.
    /// Returns the id (and minimal summary) so the client can resolve pretty URLs like
    /// <c>/projects/helix</c> back to a canonical identifier.
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<ProjectSummaryDto>> GetProjectBySlug(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return BadRequest("Slug is required");

        var normalized = slug.Trim().ToLowerInvariant();
        if (!ProjectSlugHelper.IsValid(normalized)) return NotFound();

        var viewerId = SecurityHelpers.GetUserId(User);
        var isPrivilegedViewer = SecurityHelpers.IsAdminOrCurator(User);

        var p = await _dbContext.Projects
            .AsNoTracking()
            .Include(x => x.TeamMembers)
            .Include(x => x.Boosts)
            .FirstOrDefaultAsync(x => x.Slug == normalized, ct);

        if (p is null) return NotFound();

        var access = CheckProjectAccess(ProjectViewContext.FromProject(p), viewerId, isPrivilegedViewer);
        if (access.Action != null) return access.Action;

        var boostedByMe = viewerId.HasValue && p.Boosts.Any(b => b.UserId == viewerId.Value);
        return Ok(MapToSummaryDto(p, boostedByMe));
    }

    /// <summary>
    /// Toggles the current user's boost on a viewable project.
    /// Behaves like a GitHub star: one boost per (user, project), toggling returns the new state.
    /// Updates the denormalized <see cref="Project.BoostsCount"/> atomically.
    /// </summary>
    [HttpPost("{id:guid}/toggle-boost")]
    [Authorize]
    public async Task<IActionResult> ToggleBoost(Guid id, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects
            .Include(p => p.TeamMembers)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();

        // Respect visibility — users can only boost projects they can view.
        var access = CheckProjectAccess(ProjectViewContext.FromProject(project), userId, SecurityHelpers.IsAdminOrCurator(User));
        if (access.Action != null) return access.Action;

        var existing = await _dbContext.ProjectBoosts
            .FirstOrDefaultAsync(b => b.ProjectId == id && b.UserId == userId, ct);

        bool boosted;
        if (existing != null)
        {
            _dbContext.ProjectBoosts.Remove(existing);
            project.BoostsCount = Math.Max(0, project.BoostsCount - 1);
            boosted = false;
        }
        else
        {
            _dbContext.ProjectBoosts.Add(new ProjectBoost
            {
                ProjectId = id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
            });
            project.BoostsCount += 1;
            boosted = true;
        }

        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(id);

        return Ok(new { Boosted = boosted, BoostsCount = project.BoostsCount });
    }

    #endregion

    #region Create & Update

    /// <summary>
    /// Creates a project for the authenticated user, validates size limits and slug uniqueness, creates owner membership and group chat, then publishes events.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ServiceFilter(typeof(ProfanityFilter))]
    public async Task<ActionResult<ProjectDetailsDto>> CreateProject([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var ownerId = GetRequiredUserId();

        // PC-07: Limit projects per user to prevent abuse
        const int maxProjectsPerUser = 20;
        var existingCount = await _dbContext.Projects.CountAsync(p => p.OwnerId == ownerId, ct);
        if (existingCount >= maxProjectsPerUser)
            return BadRequest($"You have reached the maximum of {maxProjectsPerUser} projects.");

        // PC-06: Validate Title and Description lengths
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError(nameof(request.Title), "Title is required.");
            return ValidationProblem(ModelState);
        }
        if (request.Title.Length > 200)
        {
            ModelState.AddModelError(nameof(request.Title), "Title must not exceed 200 characters.");
            return ValidationProblem(ModelState);
        }
        if (request.Description != null && request.Description.Length > 4000)
        {
            ModelState.AddModelError(nameof(request.Description), "Description must not exceed 4000 characters.");
            return ValidationProblem(ModelState);
        }

        // PC-01: Non-static — needs User for admin check
        var project = BuildProjectFromRequest(request, ownerId);

        var slugResult = await ResolveSlugForCreateAsync(project, request.Slug, ct);
        if (slugResult.Error != null) return slugResult.Error;
        project.Slug = slugResult.Slug;

        _dbContext.Projects.Add(project);

        var ownerMember = CreateOwnerTeamMember(project.Id, ownerId);
        _dbContext.TeamMembers.Add(ownerMember);

        // PE-02: Wrap project + member + group chat creation in a single transaction
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
        await CreateProjectGroupChatAsync(project, ownerId, ct);
        await tx.CommitAsync(ct);

        await PublishProjectCreatedEventsAsync(project, ownerId);
        await HttpContext.RequestServices.TriggerAchievementCheckAsync(ownerId, AchievementTrigger.ProjectCreated);

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, await GenerateDetailsDto(project.Id));
    }

    /// <summary>
    /// Updates project metadata when the caller owns the project, leads the team, or has curator/admin access.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ServiceFilter(typeof(ProfanityFilter))]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();

        var userId = GetRequiredUserId();
        var accessResult = await EnsureProjectEditAccessAsync(id, project, userId);
        if (accessResult != null) return accessResult;

        var validationError = ValidateUpdateRequest(request);
        if (validationError != null) return validationError;

        var visibilityResult = ValidateAndApplyVisibilityOptions(project, request);
        if (visibilityResult != null) return visibilityResult;

        var slugResult = await ApplySlugUpdateAsync(project, request.Slug, ct);
        if (slugResult != null) return slugResult;

        // PE-05: Featured can only be set by admin/curator
        var isAdmin = SecurityHelpers.IsAdminOrCurator(User);
        ApplyProjectUpdates(project, request, isAdmin);

        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(id);
        await _eventBus.PublishAsync(DomainEvents.ProjectUpdated(id, userId));

        return NoContent();
    }

    /// <summary>
    /// Changes project status for the owner when the requested status is recognized.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize]
    public async Task<ActionResult<ProjectSummaryDto>> ChangeProjectStatus(Guid id, [FromBody] ChangeStatusRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var project = await _dbContext.Projects.FindAsync(new object[] { id }, ct);
        if (project == null) return NotFound();

        if (project.OwnerId != userId)
            return Forbid();

        var status = ProjectStatus.FromString(request.Status);
        if (status == null)
            return BadRequest(new { error = "Invalid status. Valid values: draft, recruiting, active, completed, archived, cancelled" });

        var current = project.Status;
        string target = status;
        var isTransition = !string.Equals(current, target, StringComparison.OrdinalIgnoreCase);

        // DEV-116: reject ad-hoc any→any moves (e.g. completed→draft, which would silently reset
        // visibility rules). A no-op (same status) is allowed and idempotent.
        if (isTransition &&
            (!AllowedStatusTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(target)))
        {
            return Conflict(new { error = $"Cannot change project status from '{current}' to '{target}'." });
        }

        project.Status = status;
        project.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(id);

        // DEV-116: emit lifecycle events so achievements/integrations/ml react to the UI status flow.
        if (isTransition)
        {
            if (string.Equals(target, ProjectStatus.Completed, StringComparison.OrdinalIgnoreCase))
                await _eventBus.PublishAsync(DomainEvents.ProjectCompleted(id, userId));
            else if (string.Equals(target, ProjectStatus.Archived, StringComparison.OrdinalIgnoreCase))
                await _eventBus.PublishAsync(DomainEvents.ProjectArchived(id, userId));
        }

        return Ok(MapToSummaryDto(project));
    }

    /// <summary>
    /// Allowed project status transitions (DEV-116). Keys/values are the canonical lowercase status
    /// strings. Terminal-ish states are intentionally hard to leave: completed → archived only,
    /// and neither completed nor archived can revert to draft.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> AllowedStatusTransitions = new()
    {
        [ProjectStatus.Draft] = new() { ProjectStatus.Recruiting, ProjectStatus.Active, ProjectStatus.Archived, ProjectStatus.Cancelled },
        [ProjectStatus.Recruiting] = new() { ProjectStatus.Draft, ProjectStatus.Active, ProjectStatus.Completed, ProjectStatus.Archived, ProjectStatus.Cancelled },
        [ProjectStatus.Active] = new() { ProjectStatus.Recruiting, ProjectStatus.Completed, ProjectStatus.Archived, ProjectStatus.Cancelled },
        [ProjectStatus.Completed] = new() { ProjectStatus.Archived },
        [ProjectStatus.Archived] = new() { ProjectStatus.Active, ProjectStatus.Recruiting },
        [ProjectStatus.Cancelled] = new() { ProjectStatus.Draft, ProjectStatus.Recruiting },
    };

    /// <summary>
    /// Updates project visibility when the caller can edit the project and the requested visibility is supported.
    /// </summary>
    [HttpPatch("{id:guid}/visibility")]
    [Authorize]
    public async Task<IActionResult> UpdateProjectVisibility(Guid id, [FromBody] UpdateVisibilityRequest request, CancellationToken ct)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();

        var userId = GetRequiredUserId();
        var accessResult = await EnsureProjectEditAccessAsync(id, project, userId);
        if (accessResult != null) return accessResult;

        var visibility = ProjectVisibility.FromString(request.Visibility);
        if (visibility == null)
            return BadRequest("Invalid visibility. Allowed: public, private, unlisted, members, subscribers");

        project.Visibility = visibility;
        project.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(id);
        await _eventBus.PublishAsync(DomainEvents.ProjectUpdated(id, userId));

        return Ok(new { project.Visibility, project.DefaultNewsVisibility, project.DefaultFilesVisibility });
    }

    /// <summary>
    /// Updates project and default content visibility settings when the caller can edit the project.
    /// </summary>
    [HttpPatch("{id:guid}/settings")]
    [Authorize]
    public async Task<IActionResult> UpdateProjectSettings(Guid id, [FromBody] UpdateProjectSettingsRequest request, CancellationToken ct)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null) return NotFound();

        var userId = GetRequiredUserId();
        var accessResult = await EnsureProjectEditAccessAsync(id, project, userId);
        if (accessResult != null) return accessResult;

        var visibilityError = ApplyVisibilityIfProvided(request.Visibility, v => project.Visibility = v, "visibility")
            ?? ApplyVisibilityIfProvided(request.DefaultNewsVisibility, v => project.DefaultNewsVisibility = v, "default news visibility")
            ?? ApplyVisibilityIfProvided(request.DefaultFilesVisibility, v => project.DefaultFilesVisibility = v, "default files visibility");
        if (visibilityError != null) return visibilityError;

        project.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(id);
        await _eventBus.PublishAsync(DomainEvents.ProjectUpdated(id, userId));

        return Ok(new { project.Visibility, project.DefaultNewsVisibility, project.DefaultFilesVisibility });
    }

    #endregion

    #region Delete & Transfer

    /// <summary>
    /// Permanently deletes a project owned by the caller, including related activity, news, and project chat data inside a transaction.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken ct)
    {
        var project = await _dbContext.Projects
            .Include(p => p.TeamMembers)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null) return NotFound();

        var userId = GetRequiredUserId();
        if (project.OwnerId != userId)
            return Forbid("Only project owner can delete the project");

        if (IsActiveProjectWithTeam(project))
        {
            await _auditService.LogActionAsync(userId, "ProjectsController.DeleteProject", "Project", id,
                $"WARNING: Deleting active/completed project {project.Title} with team members");
        }

        var projectTitle = project.Title;

        // PE-01: Wrap multi-table deletion in a single transaction to prevent partial deletes
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        // DEV-126: bulk-delete dependents with ExecuteDeleteAsync instead of materializing whole
        // child tables into the change tracker (defense in depth alongside the DB FK cascade).
        await _dbContext.ActivityRecords.Where(ar => ar.ProjectId == id).ExecuteDeleteAsync(ct);
        await _dbContext.ProjectNewsPosts.Where(np => np.ProjectId == id).ExecuteDeleteAsync(ct);

        // Delete project group chat (convention: Conversation.Id == Project.Id)
        await _dbContext.Messages.Where(m => m.ConversationId == id).ExecuteDeleteAsync(ct);
        await _dbContext.ConversationParticipants.Where(p => p.ConversationId == id).ExecuteDeleteAsync(ct);
        await _dbContext.Conversations.Where(c => c.Id == id).ExecuteDeleteAsync(ct);

        // Remaining children (tasks, files, team members, …) are removed by the DB FK cascade.
        await _dbContext.Projects.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        await InvalidateProjectCacheAsync(id);

        await _auditService.LogActionAsync(userId, "ProjectsController.DeleteProject", "Project", id,
            $"Permanently deleted project {projectTitle} (ID: {id})");
        await _eventBus.PublishAsync(DomainEvents.ProjectDeleted(id, userId));

        return NoContent();
    }

    /// <summary>
    /// Transfers ownership to an active team member after validating the transfer in a repeatable-read transaction.
    /// </summary>
    [HttpPost("{id:guid}/transfer-ownership")]
    [Authorize]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferOwnershipRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();

        // PE-03: Use RepeatableRead to prevent TOCTOU — prevents concurrent membership changes
        await using var tx = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.RepeatableRead, ct);

        var (validated, error) = await ValidateOwnershipTransferAsync(id, userId, request, ct);
        if (error != null) return error;

        ApplyOwnershipTransfer(validated!.Project, request.NewOwnerId);
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Notify both owners (outside transaction — notifications are non-critical)
        await CreateOwnershipTransferNotificationsAsync(
            new OwnershipTransferInfo(validated.Project, validated.OldOwnerId, request.NewOwnerId, validated.NewOwner.Email), ct);

        await _auditService.LogActionAsync(userId, "ProjectsController.TransferOwnership", "Project", id,
            $"Transferred ownership from {validated.OldOwnerId} to {request.NewOwnerId}");
        await _eventBus.PublishAsync(DomainEvents.ProjectOwnershipTransferred(id, validated.OldOwnerId, request.NewOwnerId));

        return Ok(new { Message = "Ownership transferred", NewOwnerId = request.NewOwnerId });
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Reads the authenticated user's identifier claim, failing fast when authorization did not provide one.
    /// </summary>
    private Guid GetRequiredUserId() =>
        SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");

    /// <summary>
    /// Removes cached project detail and project list entries after project mutations.
    /// </summary>
    private async Task InvalidateProjectCacheAsync(Guid projectId)
    {
        await _cache.RemoveAsync($"project:{projectId}");
        await _cache.RemoveByPatternAsync("projects:*");
    }

    /// <summary>
    /// Result of project access check.
    /// </summary>
    /// <param name="FullAccess">True if user has full access (owner/member/admin), false for public view.</param>
    /// <param name="Action">Non-null if access should be denied (NotFound or Forbid action result).</param>
    private record ProjectAccessResult(bool FullAccess, ActionResult? Action);

    /// <summary>
    /// Evaluates whether a viewer gets full project data, sanitized public data, or an access-denied result.
    /// </summary>
    private ProjectAccessResult CheckProjectAccess(ProjectViewContext context, Guid? viewerId, bool isPrivilegedViewer)
    {
        var isOwner = viewerId.HasValue && context.OwnerId == viewerId.Value;
        // PE-12: Use pre-loaded member IDs instead of extra DB roundtrip
        var isTeamMember = viewerId.HasValue && context.ActiveMemberUserIds.Contains(viewerId.Value);

        var hasFullAccess = isOwner || isTeamMember || isPrivilegedViewer;

        // Draft projects are hidden from non-members
        if (context.Status == ProjectStatus.Draft && !hasFullAccess)
            return new ProjectAccessResult(false, NotFound());

        // Private/unlisted projects require membership
        if (IsRestrictedVisibility(context.Visibility) && !hasFullAccess)
            return new ProjectAccessResult(false, Forbid());

        // Public projects: members get full access, others get sanitized view
        return new ProjectAccessResult(hasFullAccess, null);
    }

    /// <summary>
    /// Allows project edits for owners, admins/curators, or active team leaders; otherwise returns a forbidden response.
    /// </summary>
    private async Task<IActionResult?> EnsureProjectEditAccessAsync(Guid projectId, Project project, Guid userId)
    {
        if (HasEditAccess(project, userId))
            return null;

        var isTeamLeader = await IsUserTeamLeaderAsync(projectId, userId);
        if (isTeamLeader)
            return null;

        return StatusCode(403, new { error = "Access denied", message = "Only project owner, team leaders, or admins can edit the project" });
    }

    /// <summary>
    /// Checks direct edit access through ownership or admin/curator claims.
    /// </summary>
    private bool HasEditAccess(Project project, Guid userId)
    {
        return project.OwnerId == userId || SecurityHelpers.IsAdminOrCurator(User);
    }

    /// <summary>
    /// Checks whether the user is an active team leader on the project.
    /// </summary>
    private async Task<bool> IsUserTeamLeaderAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.TeamMembers.AnyAsync(tm =>
            tm.ProjectId == projectId && tm.UserId == userId && tm.Status == TeamMemberStatus.Active.Value && tm.IsLeader, ct);
    }

    /// <summary>
    /// Validates update title and description length limits.
    /// </summary>
    private static string? ValidateUpdateLengths(UpdateProjectRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Title) && request.Title.Length > 200)
            return "Title too long";
        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Length > 4000)
            return "Description too long";
        return null;
    }

    /// <summary>
    /// Converts update request validation failures to a bad request response.
    /// </summary>
    private IActionResult? ValidateUpdateRequest(UpdateProjectRequest request)
    {
        var lengthError = ValidateUpdateLengths(request);
        return lengthError != null ? BadRequest(lengthError) : null;
    }

    /// <summary>
    /// Applies all mutable fields from an update request, respecting admin-only boolean fields.
    /// </summary>
    private static void ApplyProjectUpdates(Project project, UpdateProjectRequest request, bool isAdmin = false)
    {
        UpdateBasicFields(project, request);
        UpdateCollectionFields(project, request);
        UpdateOptionalFields(project, request);
        UpdateBooleanFields(project, request, isAdmin);
        UpdateDateFields(project, request);
        project.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies title, short description, description, and status updates.
    /// </summary>
    private static void UpdateBasicFields(Project project, UpdateProjectRequest request)
    {
        project.Title = request.Title?.Trim() ?? project.Title;
        project.ShortDescription = request.ShortDescription?.Trim() ?? project.ShortDescription;
        project.Description = request.Description is not null
            ? SecurityHelpers.SanitizeHtml(request.Description.Trim())
            : project.Description;

        if (!string.IsNullOrWhiteSpace(request.Status))
            project.Status = ProjectStatus.FromString(request.Status.Trim()) ?? project.Status;
    }

    /// <summary>
    /// Applies technology stack, required role, and structured open-role updates.
    /// </summary>
    private static void UpdateCollectionFields(Project project, UpdateProjectRequest request)
    {
        project.TechStack = request.TechStack != null ? NormalizeTechStack(request.TechStack) : project.TechStack;
        project.RequiredRoles = request.RequiredRoles?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? project.RequiredRoles;

        if (request.OpenRoles != null)
        {
            project.OpenRoles = request.OpenRoles
                .Where(r => !string.IsNullOrWhiteSpace(r.Role))
                .Select(r => new OpenRoleEntry
                {
                    Role = r.Role.Trim(),
                    TotalNeeded = r.TotalNeeded ?? 1,
                    HoursPerWeek = r.HoursPerWeek,
                    EquityOptional = r.EquityOptional,
                })
                .ToList();
        }
    }

    /// <summary>
    /// Applies optional scalar fields that preserve current values when omitted.
    /// </summary>
    private static void UpdateOptionalFields(Project project, UpdateProjectRequest request)
    {
        project.DifficultyLevel = request.DifficultyLevel ?? project.DifficultyLevel;
        project.ExpectedDurationDays = request.ExpectedDurationDays ?? project.ExpectedDurationDays;
        project.MaxTeamSize = request.MaxTeamSize ?? project.MaxTeamSize;
    }

    /// <summary>
    /// Applies nullable boolean updates and limits the featured flag to admins/curators.
    /// </summary>
    private static void UpdateBooleanFields(Project project, UpdateProjectRequest request, bool isAdmin)
    {
        // PE-08: Only update if explicitly provided (null = leave unchanged)
        if (request.ShowcasePublished.HasValue)
            project.ShowcasePublished = request.ShowcasePublished.Value;
        // PE-05: Featured is admin-only; non-admins cannot promote their own projects
        if (request.Featured.HasValue && isAdmin)
            project.Featured = request.Featured.Value;
    }

    /// <summary>
    /// Applies optional start and end date updates.
    /// </summary>
    private static void UpdateDateFields(Project project, UpdateProjectRequest request)
    {
        project.StartDate = request.StartDate ?? project.StartDate;
        project.EndDate = request.EndDate ?? project.EndDate;
    }

    /// <summary>
    /// Resolve the slug for a newly-created project.
    /// - If caller supplied an explicit slug, validate it (format, reserved, unique).
    /// - Otherwise auto-generate one from the title.
    /// </summary>
    private async Task<(string? Slug, ActionResult? Error)> ResolveSlugForCreateAsync(
        Project project, string? requested, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var candidate = requested.Trim().ToLowerInvariant();
            if (!ProjectSlugHelper.IsValid(candidate))
                return (null, BadRequest(new { error = "Invalid slug. Use 2-48 lowercase letters, digits, or hyphens (no leading/trailing hyphen)." }));
            if (ProjectSlugHelper.IsReserved(candidate))
                return (null, BadRequest(new { error = "This slug is reserved. Please choose a different one." }));
            var taken = await _dbContext.Projects.AsNoTracking().AnyAsync(p => p.Slug == candidate, ct);
            if (taken)
                return (null, Conflict(new { error = "This slug is already taken." }));
            return (candidate, null);
        }

        var baseSlug = ProjectSlugHelper.Slugify(project.Title);
        if (string.IsNullOrEmpty(baseSlug)) return (null, null); // Title can't yield slug — leave empty.
        var unique = await ProjectSlugHelper.EnsureUniqueAsync(_dbContext, baseSlug, excludeProjectId: null, ct);
        return (unique, null);
    }

    /// <summary>
    /// Validates and applies a slug change during project update, returning bad request or conflict for invalid or taken slugs.
    /// </summary>
    private async Task<IActionResult?> ApplySlugUpdateAsync(Project project, string? requested, CancellationToken ct)
    {
        if (requested == null) return null; // Not provided — leave unchanged.

        var trimmed = requested.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            // Empty string clears the slug.
            project.Slug = null;
            return null;
        }

        var candidate = trimmed.ToLowerInvariant();
        if (!ProjectSlugHelper.IsValid(candidate))
            return BadRequest(new { error = "Invalid slug. Use 2-48 lowercase letters, digits, or hyphens (no leading/trailing hyphen)." });
        if (ProjectSlugHelper.IsReserved(candidate))
            return BadRequest(new { error = "This slug is reserved. Please choose a different one." });

        if (string.Equals(project.Slug, candidate, StringComparison.Ordinal)) return null;

        var taken = await _dbContext.Projects.AsNoTracking()
            .AnyAsync(p => p.Slug == candidate && p.Id != project.Id, ct);
        if (taken)
            return Conflict(new { error = "This slug is already taken." });

        project.Slug = candidate;
        return null;
    }

    /// <summary>
    /// Validates and applies project, default news, and default file visibility options from an update request.
    /// </summary>
    private IActionResult? ValidateAndApplyVisibilityOptions(Project project, UpdateProjectRequest request) =>
        ApplyVisibilityIfProvided(request.Visibility, v => project.Visibility = v, "visibility")
        ?? ApplyVisibilityIfProvided(request.DefaultNewsVisibility, v => project.DefaultNewsVisibility = v, "default news visibility")
        ?? ApplyVisibilityIfProvided(request.DefaultFilesVisibility, v => project.DefaultFilesVisibility = v, "default files visibility");

    /// <summary>
    /// Creates the project group chat and adds the owner as its first participant.
    /// </summary>
    private async Task CreateProjectGroupChatAsync(Project project, Guid ownerId, CancellationToken ct)
    {
        var groupChat = new Conversation
        {
            Id = project.Id,
            Type = ConversationType.Group,
            Title = project.Title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Conversations.Add(groupChat);

        _dbContext.ConversationParticipants.Add(new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = groupChat.Id,
            UserId = ownerId,
            JoinedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>Validated entities and identifiers needed to apply a project ownership transfer.</summary>
    private record OwnershipTransferValidation(Project Project, User NewOwner, Guid OldOwnerId);
    /// <summary>Notification data emitted after project ownership transfer completes.</summary>
    private record OwnershipTransferInfo(Project Project, Guid OldOwnerId, Guid NewOwnerId, string NewOwnerEmail);

    /// <summary>
    /// Validates ownership transfer preconditions: project exists, caller owns it, new owner is active, and new owner is an active member.
    /// </summary>
    private async Task<(OwnershipTransferValidation? Result, IActionResult? Error)> ValidateOwnershipTransferAsync(
        Guid projectId, Guid userId, TransferOwnershipRequest request, CancellationToken ct)
    {
        var project = await _dbContext.Projects
            .Include(p => p.TeamMembers)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return (null, NotFound());
        if (project.OwnerId != userId) return (null, Forbid());

        var newOwner = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.NewOwnerId && u.IsActive, ct);
        if (newOwner == null) return (null, NotFound("New owner not found or inactive"));

        // PE-03: DB query instead of in-memory snapshot to avoid stale data
        var isMember = await _dbContext.TeamMembers
            .AnyAsync(tm => tm.ProjectId == projectId && tm.UserId == request.NewOwnerId
                && tm.Status == TeamMemberStatus.Active.Value, ct);
        if (!isMember) return (null, BadRequest("New owner must be an active member of the project"));

        return (new OwnershipTransferValidation(project, newOwner, project.OwnerId), null);
    }

    /// <summary>Applies ownership transfer to the project and promotes the new owner to team leader when present.</summary>
    private static void ApplyOwnershipTransfer(Project project, Guid newOwnerId)
    {
        project.OwnerId = newOwnerId;
        project.UpdatedAt = DateTime.UtcNow;
        var newOwnerMember = project.TeamMembers.FirstOrDefault(tm => tm.UserId == newOwnerId);
        if (newOwnerMember != null) newOwnerMember.IsLeader = true;
    }

    /// <summary>
    /// Creates notifications for the previous and new project owners after ownership transfer.
    /// </summary>
    private async Task CreateOwnershipTransferNotificationsAsync(OwnershipTransferInfo info, CancellationToken ct)
    {
        _dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = info.OldOwnerId,
            Type = "project",
            Title = "Project ownership transferred",
            Content = $"Ownership of project '{info.Project.Title}' transferred to user {info.NewOwnerEmail}.",
            RelatedEntityType = "Project",
            RelatedEntityId = info.Project.Id,
            Priority = "medium",
            CreatedAt = DateTime.UtcNow
        });

        _dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = info.NewOwnerId,
            Type = "project",
            Title = "You became the project owner",
            Content = $"You have been transferred ownership of project '{info.Project.Title}'.",
            RelatedEntityType = "Project",
            RelatedEntityId = info.Project.Id,
            Priority = "high",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Loads a project with related data and maps it to a details DTO for create responses.
    /// </summary>
    private async Task<ProjectDetailsDto> GenerateDetailsDto(Guid id, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.TeamMembers).ThenInclude(tm => tm.User)
            .Include(p => p.Tasks)
            .Include(p => p.Invitations).ThenInclude(i => i.Invitee)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project == null)
            throw new InvalidOperationException($"Failed to generate ProjectDetailsDto for project {id} - project not found");

        return MapToDetailsDto(project);
    }

    /// <summary>
    /// Maps a loaded project entity to its detailed API DTO.
    /// </summary>
    private static ProjectDetailsDto MapToDetailsDto(Project p, bool boostedByMe = false)
    {
        var openRolesCount = ComputeOpenRolesCount(p);
        return new ProjectDetailsDto(
            p.Id, p.Title, p.ShortDescription, p.Description, p.TechStack, p.Status, p.Visibility,
            p.DefaultNewsVisibility ?? p.Visibility, p.DefaultFilesVisibility ?? p.Visibility, p.OwnerId,
            p.DifficultyLevel, p.ExpectedDurationDays, p.ShowcasePublished, p.Featured,
            p.MaxTeamSize, p.Rating, p.CreatedAt, p.UpdatedAt, p.RequiredRoles, p.StartDate, p.EndDate,
            p.TeamMembers.Select(tm => new ProjectTeamMemberDto(
                tm.Id, tm.UserId, tm.User?.Email ?? string.Empty, tm.User?.FullName, tm.Role, tm.Contribution, tm.JoinedAt)).ToList(),
            p.Tasks.Select(t => new ProjectTaskDto(t.Id, t.Title, t.Status, t.AssignedToUserId, t.Deadline, t.Priority, t.Tags)).OrderBy(t => t.Status).ToList(),
            p.Invitations.OrderByDescending(i => i.CreatedAt).Select(i => new ProjectInvitationDto(
                i.Id, i.InviteeId, i.Invitee?.Email ?? string.Empty, i.Invitee?.FullName, i.Role, i.Status, i.CreatedAt, i.RespondedAt)).ToList(),
            p.OpenRoles?.Select(r => new OpenRoleDto(r.Role, r.TotalNeeded, r.HoursPerWeek, r.EquityOptional)).ToList(),
            p.Slug, p.BoostsCount, openRolesCount, boostedByMe
        );
    }

    /// <summary>
    /// Maps a loaded project entity to its list summary DTO.
    /// </summary>
    private static ProjectSummaryDto MapToSummaryDto(Project p, bool boostedByMe = false)
    {
        return new ProjectSummaryDto(
            p.Id, p.Title, p.ShortDescription, p.Description, p.TechStack, p.Status, p.Visibility,
            p.OwnerId, p.DifficultyLevel, p.ExpectedDurationDays, p.ShowcasePublished, p.Featured,
            p.TeamMembers?.Count ?? 0, p.MaxTeamSize, p.Rating, p.CreatedAt, p.UpdatedAt,
            p.Slug, p.BoostsCount, ComputeOpenRolesCount(p), boostedByMe);
    }

    /// <summary>
    /// Count of unfilled role seats across the structured <see cref="Project.OpenRoles"/> list.
    /// For each role we subtract the number of active team members currently holding that role.
    /// </summary>
    private static int ComputeOpenRolesCount(Project p)
    {
        if (p.OpenRoles == null || p.OpenRoles.Count == 0) return 0;

        var activeByRole = p.TeamMembers
            .Where(tm => tm.Status == TeamMemberStatus.Active.Value)
            .GroupBy(tm => (tm.Role ?? string.Empty).Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        var total = 0;
        foreach (var r in p.OpenRoles)
        {
            var key = (r.Role ?? string.Empty).Trim().ToLowerInvariant();
            activeByRole.TryGetValue(key, out var assigned);
            total += Math.Max(r.TotalNeeded - assigned, 0);
        }
        return total;
    }

    /// <summary>
    /// Removes member identities, tasks, and invitations from project details for public viewers without full access.
    /// </summary>
    private static ProjectDetailsDto SanitizeProjectForPublicView(ProjectDetailsDto dto)
    {
        // PE-07: Strip UserId, Email, FullName from team members for public view
        var sanitizedTeam = dto.Team.Select(tm => tm with
        {
            UserId = Guid.Empty,
            Email = string.Empty,
            FullName = null
        }).ToList();
        return dto with
        {
            Team = sanitizedTeam,
            Tasks = Array.Empty<ProjectTaskDto>(),
            Invitations = Array.Empty<ProjectInvitationDto>()
        };
    }

    /// <summary>
    /// Parses a visibility string or falls back to public visibility.
    /// </summary>
    private static ProjectVisibility NormalizeVisibility(string? value) =>
        ProjectVisibility.FromString(value) ?? ProjectVisibility.Public;

    /// <summary>
    /// Placeholder visibility allow-list check retained for compatibility with existing validation flow.
    /// </summary>
    private static bool IsAllowedVisibility(ProjectVisibility visibility) => true;

    /// <summary>
    /// Check if project is active/completed and has team members (used for deletion warnings).
    /// </summary>
    private static bool IsActiveProjectWithTeam(Project project) =>
        (project.Status == ProjectStatus.Active || project.Status == ProjectStatus.Completed) && project.TeamMembers.Any();

    /// <summary>
    /// Check if visibility restricts public access.
    /// </summary>
    private static bool IsRestrictedVisibility(string visibility) =>
        ProjectVisibility.FromString(visibility)?.IsRestricted ?? false;

    /// <summary>
    /// Validate and apply visibility setting if provided.
    /// Returns BadRequest result if invalid, null if OK or skipped.
    /// </summary>
    private IActionResult? ApplyVisibilityIfProvided(string? value, Action<string> setter, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var visibility = ProjectVisibility.FromString(value);
        if (visibility == null)
            return BadRequest($"Invalid {fieldName}. Allowed: public, private, unlisted, members, subscribers");
        setter(visibility);
        return null;
    }

    /// <summary>
    /// Trims, removes empty entries, and de-duplicates technology labels case-insensitively.
    /// </summary>
    private static List<string> NormalizeTechStack(List<string>? techStack) =>
        techStack?.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

    /// <summary>
    /// Trims required role labels and removes empty entries.
    /// </summary>
    private static List<string> NormalizeRequiredRoles(List<string>? roles) =>
        roles?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>();

    /// <summary>Resolved project, news, and file visibility values for a new project.</summary>
    private record ProjectVisibilityConfig(string Visibility, string DefaultNewsVisibility, string DefaultFilesVisibility);

    /// <summary>
    /// Resolves project visibility defaults for a create request.
    /// </summary>
    private static ProjectVisibilityConfig ResolveVisibilityConfig(CreateProjectRequest request)
    {
        var baseVis = NormalizeVisibility(request.Visibility);
        return new ProjectVisibilityConfig(
            baseVis,
            NormalizeVisibility(request.DefaultNewsVisibility ?? baseVis),
            NormalizeVisibility(request.DefaultFilesVisibility ?? baseVis));
    }

    // PC-01: No longer static — requires access to User for admin check
    /// <summary>
    /// Builds a new project entity from a validated create request and caller identity.
    /// </summary>
    private Project BuildProjectFromRequest(CreateProjectRequest request, Guid ownerId)
    {
        var isAdmin = SecurityHelpers.IsAdminOrCurator(User);
        var vis = ResolveVisibilityConfig(request);

        return new Project
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            ShortDescription = request.ShortDescription?.Trim(),
            Description = SecurityHelpers.SanitizeHtml(request.Description?.Trim() ?? string.Empty),
            TechStack = NormalizeTechStack(request.TechStack),
            RequiredRoles = NormalizeRequiredRoles(request.RequiredRoles),
            OpenRoles = request.OpenRoles?
                .Where(r => !string.IsNullOrWhiteSpace(r.Role))
                .Select(r => new OpenRoleEntry
                {
                    Role = r.Role.Trim(),
                    TotalNeeded = r.TotalNeeded ?? 1,
                    HoursPerWeek = r.HoursPerWeek,
                    EquityOptional = r.EquityOptional,
                })
                .ToList(),
            // PC-03: Validate status — only draft/recruiting allowed at creation
            Status = ResolveInitialStatus(request.Status),
            Visibility = vis.Visibility,
            // PC-05: Use dedicated fields instead of request.Visibility for both
            DefaultNewsVisibility = vis.DefaultNewsVisibility,
            DefaultFilesVisibility = vis.DefaultFilesVisibility,
            OwnerId = ownerId,
            DifficultyLevel = request.DifficultyLevel,
            ExpectedDurationDays = request.ExpectedDurationDays,
            // PC-01: Only admins can set Featured/ShowcasePublished
            ShowcasePublished = isAdmin && request.ShowcasePublished,
            Featured = isAdmin && request.Featured,
            MaxTeamSize = request.MaxTeamSize,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // PC-03: Restrict initial status to draft or recruiting only
    /// <summary>
    /// Resolves the initial project status, accepting only draft or recruiting and defaulting to draft.
    /// </summary>
    private static string ResolveInitialStatus(string? requestedStatus)
    {
        if (string.IsNullOrWhiteSpace(requestedStatus))
            return ProjectStatus.Draft;
        var trimmed = requestedStatus.Trim();
        return trimmed.Equals(ProjectStatus.Draft, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(ProjectStatus.Recruiting, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : ProjectStatus.Draft;
    }

    /// <summary>
    /// Creates the owner's initial active team membership with full project management permissions.
    /// </summary>
    private static TeamMember CreateOwnerTeamMember(Guid projectId, Guid ownerId)
    {
        return new TeamMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = ownerId,
            // PC-11: Use constants instead of string literals
            Role = "owner",
            Status = TeamMemberStatus.Active,
            IsLeader = true,
            JoinedAt = DateTime.UtcNow,
            CanManageFiles = true,
            CanManageGallery = true,
            CanManageTasks = true,
            CanPublishNews = true
        };
    }

    /// <summary>
    /// Publishes the project-created domain event and logs the owner's creation activity.
    /// </summary>
    private async Task PublishProjectCreatedEventsAsync(Project project, Guid ownerId)
    {
        await _eventBus.PublishAsync(DomainEvents.ProjectCreated(project.Id, ownerId));
        await _projectServices.ActivityLog.LogUserEventAsync(ownerId, "user.created_project",
            $"Created project {project.Title}",
            visibility: ActivityVisibilityHelper.FromProjectVisibility(project.Visibility),
            payload: new { project.Id });
    }

    #endregion
}
