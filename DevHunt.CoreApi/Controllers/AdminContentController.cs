using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Admin content management: projects, news, comments, showcase.
/// </summary>
[ApiController]
[Route("api/admin/content")]
[Authorize]
public class AdminContentController : ControllerBase
{
    private readonly DevHuntDbContext _db;
    private readonly IAuditService _auditService;

    /// <summary>
    /// Creates the admin content controller with persistence and audit logging services.
    /// </summary>
    /// <param name="db">Database context used for content moderation queries and mutations.</param>
    /// <param name="auditService">Audit service used to record destructive or privileged content actions.</param>
    public AdminContentController(DevHuntDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId() =>
        SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");

    /// <summary>
    /// Checks the database for an active admin, curator, or superadmin matching the current claims principal.
    /// </summary>
    private async Task<bool> IsAdminOrCuratorAsync(CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return false;
        var role = await _db.Users.AsNoTracking().Where(u => u.Id == userId.Value && u.IsActive).Select(u => u.Role).FirstOrDefaultAsync(ct);
        return role is UserRoles.Admin or UserRoles.Curator or UserRoles.SuperAdmin;
    }

    // ========================================================================
    // Stats
    // ========================================================================

    /// <summary>
    /// Returns aggregate counts for projects, news posts, comments, and showcase entries.
    /// </summary>
    /// <param name="ct">Cancellation token for count queries.</param>
    /// <returns>Forbids non-admin/curator callers; otherwise returns content totals.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetContentStats(CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        return Ok(new
        {
            projects = await _db.Projects.CountAsync(ct),
            news = await _db.ProjectNewsPosts.CountAsync(ct),
            showcaseComments = await _db.ShowcaseComments.CountAsync(ct),
            newsComments = await _db.NewsPostComments.CountAsync(ct),
            showcase = await _db.ShowcaseProjects.CountAsync(ct)
        });
    }

    // ========================================================================
    // Projects
    // ========================================================================

    /// <summary>
    /// Describes project fields an admin or curator may patch.
    /// </summary>
    /// <param name="Title">Optional replacement project title.</param>
    /// <param name="Description">Optional replacement project description.</param>
    /// <param name="Status">Optional replacement project status.</param>
    /// <param name="Visibility">Optional replacement visibility value.</param>
    /// <param name="Featured">Optional featured-state override.</param>
    public record UpdateProjectRequest(string? Title, string? Description, string? Status, string? Visibility, bool? Featured);

    /// <summary>
    /// Lists projects with optional status, visibility, featured, title-search, and pagination filters.
    /// </summary>
    /// <param name="status">Optional exact project status filter.</param>
    /// <param name="visibility">Optional exact visibility filter.</param>
    /// <param name="featured">Optional featured-state filter.</param>
    /// <param name="search">Optional case-insensitive title search.</param>
    /// <param name="page">Page number to return.</param>
    /// <param name="pageSize">Number of projects per page.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>Forbids non-admin/curator callers; otherwise returns paged project summaries.</returns>
    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(
        [FromQuery] string? status,
        [FromQuery] string? visibility,
        [FromQuery] bool? featured,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var query = _db.Projects.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(status)) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrEmpty(visibility)) query = query.Where(p => p.Visibility == visibility);
        if (featured.HasValue) query = query.Where(p => p.Featured == featured.Value);
        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var projects = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Title, p.Status, p.Visibility, p.Featured, p.ShowcasePublished,
                p.CreatedAt, p.UpdatedAt,
                OwnerName = _db.Users.Where(u => u.Id == p.OwnerId).Select(u => u.FullName ?? u.Email).FirstOrDefault(),
                TeamCount = _db.TeamMembers.Count(tm => tm.ProjectId == p.Id && tm.Status == TeamMemberStatus.Active.Value),
                TaskCount = _db.Tasks.Count(t => t.ProjectId == p.Id)
            })
            .ToListAsync(ct);

        return Ok(new { data = projects, pagination = new { page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize) } });
    }

    /// <summary>
    /// Returns detailed project metadata, owner information, and related content counts.
    /// </summary>
    /// <param name="id">Project identifier to inspect.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>Forbids non-admin/curator callers, returns not found for a missing project, and otherwise returns details.</returns>
    [HttpGet("projects/{id:guid}")]
    public async Task<IActionResult> GetProjectDetail(Guid id, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project == null) return NotFound();

        var owner = await _db.Users.AsNoTracking().Where(u => u.Id == project.OwnerId).Select(u => new { u.Id, u.FullName, u.Email }).FirstOrDefaultAsync(ct);

        return Ok(new
        {
            project.Id, project.Title, project.Description, project.ShortDescription,
            project.Status, project.Visibility, project.Featured, project.ShowcasePublished,
            project.TechStack, project.DifficultyLevel, project.MaxTeamSize,
            project.CreatedAt, project.UpdatedAt, project.StartDate, project.EndDate,
            Owner = owner,
            TeamCount = await _db.TeamMembers.CountAsync(tm => tm.ProjectId == id && tm.Status == TeamMemberStatus.Active.Value, ct),
            TaskCount = await _db.Tasks.CountAsync(t => t.ProjectId == id, ct),
            NewsCount = await _db.ProjectNewsPosts.CountAsync(n => n.ProjectId == id, ct)
        });
    }

    /// <summary>
    /// Applies supplied project field changes and audits the changed fields.
    /// </summary>
    /// <param name="id">Project identifier to update.</param>
    /// <param name="req">Patch payload; null properties are ignored.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids non-admin/curator callers, returns not found for missing projects, and returns OK when no changes or after saving.</returns>
    [HttpPut("projects/{id:guid}")]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectRequest req, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project == null) return NotFound();

        var changes = new List<string>();
        if (req.Title != null && req.Title != project.Title) { project.Title = req.Title; changes.Add("title"); }
        if (req.Description != null && req.Description != project.Description) { project.Description = req.Description; changes.Add("description"); }
        if (req.Status != null && req.Status != project.Status) { project.Status = req.Status; changes.Add($"status→{req.Status}"); }
        if (req.Visibility != null && req.Visibility != project.Visibility) { project.Visibility = req.Visibility; changes.Add($"visibility→{req.Visibility}"); }
        if (req.Featured.HasValue && req.Featured.Value != project.Featured) { project.Featured = req.Featured.Value; changes.Add($"featured→{req.Featured.Value}"); }

        if (changes.Count == 0) return Ok();

        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var adminId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminId, "AdminContent.UpdateProject", "Project", id,
            $"Updated: {string.Join(", ", changes)}");

        return Ok();
    }

    /// <summary>
    /// Deletes a project, its activity records, news posts, and chat conversation before auditing the removal.
    /// </summary>
    /// <param name="id">Project identifier to delete.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids non-admin/curator callers, returns not found for missing projects, and returns no content after deletion.</returns>
    [HttpDelete("projects/{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var project = await _db.Projects
            .Include(p => p.TeamMembers)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project == null) return NotFound();

        var title = project.Title;
        var adminId = GetRequiredUserId();

        // Cascade deletes
        var activityRecords = await _db.ActivityRecords.Where(a => a.ProjectId == id).ToListAsync(ct);
        if (activityRecords.Count > 0) _db.ActivityRecords.RemoveRange(activityRecords);

        var newsPosts = await _db.ProjectNewsPosts.Where(n => n.ProjectId == id).ToListAsync(ct);
        if (newsPosts.Count > 0) _db.ProjectNewsPosts.RemoveRange(newsPosts);

        // Delete project chat
        var chat = await _db.Conversations.Include(c => c.Participants).Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (chat != null)
        {
            _db.Messages.RemoveRange(chat.Messages);
            _db.ConversationParticipants.RemoveRange(chat.Participants);
            _db.Conversations.Remove(chat);
        }

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(adminId, "AdminContent.DeleteProject", "Project", id,
            $"Deleted project \"{title}\"", null, "critical");

        return NoContent();
    }

    // ========================================================================
    // News
    // ========================================================================

    /// <summary>
    /// Lists project news posts with optional project, title-search, and pagination filters.
    /// </summary>
    /// <param name="projectId">Optional project identifier filter.</param>
    /// <param name="search">Optional case-insensitive title search.</param>
    /// <param name="page">Page number to return.</param>
    /// <param name="pageSize">Number of news posts per page.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>Forbids non-admin/curator callers; otherwise returns paged news summaries.</returns>
    [HttpGet("news")]
    public async Task<IActionResult> GetNewsPosts(
        [FromQuery] Guid? projectId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var query = _db.ProjectNewsPosts.AsNoTracking().AsQueryable();
        if (projectId.HasValue) query = query.Where(n => n.ProjectId == projectId.Value);
        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLower();
            query = query.Where(n => n.Title.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var news = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new
            {
                n.Id, n.Title, n.ProjectId,
                ProjectTitle = _db.Projects.Where(p => p.Id == n.ProjectId).Select(p => p.Title).FirstOrDefault(),
                AuthorName = _db.Users.Where(u => u.Id == n.AuthorId).Select(u => u.FullName ?? u.Email).FirstOrDefault(),
                n.Visibility, n.CreatedAt,
                LikesCount = _db.NewsPostLikes.Count(l => l.NewsPostId == n.Id),
                CommentsCount = _db.NewsPostComments.Count(c => c.NewsPostId == n.Id)
            })
            .ToListAsync(ct);

        return Ok(new { data = news, pagination = new { page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize) } });
    }

    /// <summary>
    /// Deletes a news post with its likes and comments, then audits the action.
    /// </summary>
    /// <param name="id">News post identifier to delete.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids non-admin/curator callers, returns not found for missing posts, and returns no content after deletion.</returns>
    [HttpDelete("news/{id:guid}")]
    public async Task<IActionResult> DeleteNewsPost(Guid id, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var post = await _db.ProjectNewsPosts.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (post == null) return NotFound();

        // Delete related likes and comments
        var likes = await _db.NewsPostLikes.Where(l => l.NewsPostId == id).ToListAsync(ct);
        var comments = await _db.NewsPostComments.Where(c => c.NewsPostId == id).ToListAsync(ct);
        if (likes.Count > 0) _db.NewsPostLikes.RemoveRange(likes);
        if (comments.Count > 0) _db.NewsPostComments.RemoveRange(comments);

        _db.ProjectNewsPosts.Remove(post);
        await _db.SaveChangesAsync(ct);

        var adminId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminId, "AdminContent.DeleteNews", "ProjectNewsPost", id,
            $"Deleted news post \"{post.Title}\"");

        return NoContent();
    }

    // ========================================================================
    // Comments
    // ========================================================================

    /// <summary>
    /// Lists showcase and news comments together with optional type, text-search, and pagination filters.
    /// </summary>
    /// <param name="type">Optional comment source filter: showcase or news.</param>
    /// <param name="search">Optional case-insensitive search over content and author name.</param>
    /// <param name="page">Page number to return.</param>
    /// <param name="pageSize">Number of comments per page.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>Forbids non-admin/curator callers; otherwise returns a paged combined comment list.</returns>
    [HttpGet("comments")]
    public async Task<IActionResult> GetComments(
        [FromQuery] string? type,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var showcaseComments = type is null or "showcase"
            ? await _db.ShowcaseComments.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    Type = "showcase",
                    Content = c.Content,
                    AuthorName = _db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.FullName ?? u.Email).FirstOrDefault() ?? "Unknown",
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync(ct)
            : new List<CommentDto>();

        var newsComments = type is null or "news"
            ? await _db.NewsPostComments.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    Type = "news",
                    Content = c.Content,
                    AuthorName = _db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.FullName ?? u.Email).FirstOrDefault() ?? "Unknown",
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync(ct)
            : new List<CommentDto>();

        var all = showcaseComments.Concat(newsComments).OrderByDescending(c => c.CreatedAt).ToList();

        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLower();
            all = all.Where(c => c.Content.ToLower().Contains(s) || c.AuthorName.ToLower().Contains(s)).ToList();
        }

        var total = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new { data = paged, pagination = new { page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize) } });
    }

    /// <summary>
    /// Deletes a showcase or news comment selected by the required type query parameter.
    /// </summary>
    /// <param name="id">Comment identifier to delete.</param>
    /// <param name="type">Comment source; must be showcase or news.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids non-admin/curator callers, returns not found for missing comments, rejects invalid types, and returns no content after auditing.</returns>
    [HttpDelete("comments/{id:guid}")]
    public async Task<IActionResult> DeleteComment(Guid id, [FromQuery] string type, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var adminId = GetRequiredUserId();

        if (type == "showcase")
        {
            var comment = await _db.ShowcaseComments.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (comment == null) return NotFound();
            _db.ShowcaseComments.Remove(comment);
        }
        else if (type == "news")
        {
            var comment = await _db.NewsPostComments.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (comment == null) return NotFound();
            _db.NewsPostComments.Remove(comment);
        }
        else
        {
            return BadRequest("type parameter required: showcase or news");
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogActionAsync(adminId, "AdminContent.DeleteComment", "Comment", id, $"Deleted {type} comment");

        return NoContent();
    }

    // ========================================================================
    // Showcase
    // ========================================================================

    /// <summary>
    /// Lists showcase entries with project title, publish time, comment count, and pagination metadata.
    /// </summary>
    /// <param name="page">Page number to return.</param>
    /// <param name="pageSize">Number of showcase entries per page.</param>
    /// <param name="ct">Cancellation token for database queries.</param>
    /// <returns>Forbids non-admin/curator callers; otherwise returns paged showcase summaries.</returns>
    [HttpGet("showcase")]
    public async Task<IActionResult> GetShowcase(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var query = _db.ShowcaseProjects.AsNoTracking();
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.PublishedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.ProjectId,
                ProjectTitle = _db.Projects.Where(p => p.Id == s.ProjectId).Select(p => p.Title).FirstOrDefault(),
                s.PublishedAt,
                CommentsCount = _db.ShowcaseComments.Count(c => c.ShowcaseProjectId == s.Id)
            })
            .ToListAsync(ct);

        return Ok(new { data = items, pagination = new { page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize) } });
    }

    /// <summary>
    /// Deletes a showcase entry and its comments, then audits the removal.
    /// </summary>
    /// <param name="id">Showcase entry identifier to delete.</param>
    /// <param name="ct">Cancellation token for database and audit operations.</param>
    /// <returns>Forbids non-admin/curator callers, returns not found for missing entries, and returns no content after deletion.</returns>
    [HttpDelete("showcase/{id:guid}")]
    public async Task<IActionResult> DeleteShowcase(Guid id, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync()) return Forbid();

        var showcase = await _db.ShowcaseProjects.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (showcase == null) return NotFound();

        // Delete related comments
        var comments = await _db.ShowcaseComments.Where(c => c.ShowcaseProjectId == id).ToListAsync(ct);
        if (comments.Count > 0) _db.ShowcaseComments.RemoveRange(comments);

        _db.ShowcaseProjects.Remove(showcase);
        await _db.SaveChangesAsync(ct);

        var adminId = GetRequiredUserId();
        await _auditService.LogActionAsync(adminId, "AdminContent.DeleteShowcase", "ShowcaseProject", id,
            $"Removed showcase entry for project {showcase.ProjectId}");

        return NoContent();
    }

    /// <summary>Internal projection used to merge showcase and news comments in a single list.</summary>
    private class CommentDto
    {
        /// <summary>
        /// Comment identifier.
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// Source of the comment, either showcase or news.
        /// </summary>
        public string Type { get; set; } = "";
        /// <summary>
        /// Comment body text.
        /// </summary>
        public string Content { get; set; } = "";
        /// <summary>
        /// Display name resolved for the comment author.
        /// </summary>
        public string AuthorName { get; set; } = "";
        /// <summary>
        /// Timestamp when the comment was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
