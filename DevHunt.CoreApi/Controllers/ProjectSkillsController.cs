using System.ComponentModel.DataAnnotations;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Project technology stack management (REST namespace /api/projects/{projectId}/skills).
/// </summary>
[ApiController]
[Route("api/projects")]
public class ProjectSkillsController : BaseProjectController
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectSkillsController"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="auditService">The audit service.</param>
    /// <param name="notificationService">The notification service client.</param>
    /// <param name="eventBus">The event bus service.</param>
    /// <param name="cache">The cache service.</param>
    public ProjectSkillsController(
        DevHuntDbContext dbContext,
        IAuditService auditService,
        INotificationServiceClient notificationService,
        IEventBusService eventBus,
        ICacheService cache)
        : base(dbContext, auditService, notificationService, eventBus, cache)
    {
    }

    /// <summary>
    /// Gets project skills in category/name order, returning not found for missing projects and forbidding private skills from outsiders.
    /// </summary>
    [HttpGet("{projectId:guid}/skills")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjectSkills(Guid projectId, CancellationToken ct = default)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Visibility })
            .FirstOrDefaultAsync(ct);

        if (project == null)
        {
            return NotFound("Project not found");
        }

        var userId = GetOwnerId();
        if (!await CanViewProjectSkillsAsync(projectId, project.Visibility, userId))
        {
            return Forbid("Project skills are private");
        }

        var skills = await _dbContext.ProjectTechStacks
            .AsNoTracking()
            .Include(ts => ts.Skill)
            .Where(ts => ts.ProjectId == projectId)
            .OrderBy(ts => ts.Skill.Category)
            .ThenBy(ts => ts.Skill.Name)
            .Select(ts => new
            {
                ts.Id,
                ts.IsRequired,
                ts.ProficiencyRequired,
                Skill = new
                {
                    ts.Skill.Id,
                    ts.Skill.Name,
                    ts.Skill.Category,
                    ts.Skill.Description,
                    ts.Skill.IconUrl
                }
            })
            .ToListAsync(ct);

        return Ok(new { Items = skills, Count = skills.Count });
    }

    /// <summary>
    /// Adds a skill to the project for the owner, rejecting missing projects, missing skills, and duplicate project skill rows.
    /// </summary>
    [HttpPost("{projectId:guid}/skills")]
    [Authorize]
    public async Task<IActionResult> AddProjectSkill(Guid projectId, [FromBody] ProjectSkillRequest request, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        if (project.OwnerId != userId)
        {
            return Forbid("Only project owner can modify project skills");
        }

        var skill = await _dbContext.Skills.FindAsync(new object[] { request.SkillId }, ct);
        if (skill == null)
        {
            return NotFound("Skill not found");
        }

        var exists = await _dbContext.ProjectTechStacks
            .AnyAsync(ts => ts.ProjectId == projectId && ts.SkillId == request.SkillId, ct);
        if (exists)
        {
            return Conflict("Skill already added to project");
        }

        var techStack = new ProjectTechStack
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SkillId = request.SkillId,
            IsRequired = request.IsRequired,
            ProficiencyRequired = request.ProficiencyRequired
        };

        _dbContext.ProjectTechStacks.Add(techStack);
        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        await _auditService.LogActionAsync(
            userId,
            nameof(ProjectSkillsController) + ".AddProjectSkill",
            nameof(ProjectTechStack),
            techStack.Id,
            $"Added skill {skill.Name} to project {projectId}");

        return CreatedAtAction(nameof(GetProjectSkills), new { projectId }, new
        {
            techStack.Id,
            techStack.SkillId,
            techStack.IsRequired,
            techStack.ProficiencyRequired
        });
    }

    /// <summary>
    /// Updates required/proficiency metadata for a project skill when the caller owns the project.
    /// </summary>
    [HttpPut("{projectId:guid}/skills/{projectSkillId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateProjectSkill(
        Guid projectId,
        Guid projectSkillId,
        [FromBody] UpdateProjectSkillRequest request, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        if (project.OwnerId != userId)
        {
            return Forbid("Only project owner can modify project skills");
        }

        var techStack = await _dbContext.ProjectTechStacks
            .FirstOrDefaultAsync(ts => ts.Id == projectSkillId && ts.ProjectId == projectId, ct);
        if (techStack == null)
        {
            return NotFound("Project skill not found");
        }

        techStack.IsRequired = request.IsRequired;
        techStack.ProficiencyRequired = request.ProficiencyRequired;

        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        await _auditService.LogActionAsync(
            userId,
            nameof(ProjectSkillsController) + ".UpdateProjectSkill",
            nameof(ProjectTechStack),
            techStack.Id,
            "Updated project skill settings");

        return Ok(new
        {
            techStack.Id,
            techStack.SkillId,
            techStack.IsRequired,
            techStack.ProficiencyRequired
        });
    }

    /// <summary>
    /// Removes a project skill when the caller owns the project.
    /// </summary>
    [HttpDelete("{projectId:guid}/skills/{projectSkillId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveProjectSkill(Guid projectId, Guid projectSkillId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var project = await _dbContext.Projects.FindAsync(new object[] { projectId }, ct);
        if (project == null) return NotFound("Project not found");

        if (project.OwnerId != userId)
        {
            return Forbid("Only project owner can modify project skills");
        }

        var techStack = await _dbContext.ProjectTechStacks
            .FirstOrDefaultAsync(ts => ts.Id == projectSkillId && ts.ProjectId == projectId, ct);
        if (techStack == null)
        {
            return NotFound("Project skill not found");
        }

        _dbContext.ProjectTechStacks.Remove(techStack);
        await _dbContext.SaveChangesAsync(ct);
        await InvalidateProjectCacheAsync(projectId);

        await _auditService.LogActionAsync(
            userId,
            nameof(ProjectSkillsController) + ".RemoveProjectSkill",
            nameof(ProjectTechStack),
            techStack.Id,
            "Removed project skill");

        return NoContent();
    }

    /// <summary>
    /// Allows public skill visibility for non-private projects and limits private project skills to project members.
    /// </summary>
    private async Task<bool> CanViewProjectSkillsAsync(Guid projectId, string visibility, Guid userId)
    {
        if (!string.Equals(visibility, "private", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (userId == Guid.Empty)
        {
            return false;
        }

        return await HasProjectAccessAsync(projectId, userId);
    }
}

/// <summary>Request to add a skill to a project.</summary>
public class ProjectSkillRequest
{
    /// <summary>Skill identifier to attach.</summary>
    [Required]
    public Guid SkillId { get; set; }

    /// <summary>Whether the skill is required for the project.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Required proficiency label (optional).</summary>
    [MaxLength(50)]
    public string? ProficiencyRequired { get; set; }
}

/// <summary>Request to update project skill properties.</summary>
public class UpdateProjectSkillRequest
{
    /// <summary>Whether the skill is required.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Updated proficiency label (optional).</summary>
    [MaxLength(50)]
    public string? ProficiencyRequired { get; set; }
}
