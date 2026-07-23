using DevHunt.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Central metadata storefront (skills/badges/projects) under `/api/metadata`.
/// </summary>
[ApiController]
[Route("api/metadata")]
[AllowAnonymous]
public class MetadataController : ControllerBase
{
    private readonly DevHuntDbContext _context;
    private readonly ILogger<MetadataController> _logger;

    /// <summary>
    /// Creates the metadata controller with read-only catalog access and diagnostics.
    /// </summary>
    /// <param name="context">Database context used to query distinct metadata values.</param>
    /// <param name="logger">Logger used for metadata request diagnostics.</param>
    public MetadataController(DevHuntDbContext context, ILogger<MetadataController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Returns the metadata sections exposed by this controller and their routes.
    /// </summary>
    [HttpGet]
    public IActionResult GetMetadataIndex()
    {
        var sections = new[]
        {
            new { Name = "skills.categories", Route = "/api/metadata/skills/categories" },
            new { Name = "badges.categories", Route = "/api/metadata/badges/categories" },
            new { Name = "projects.statuses", Route = "/api/metadata/projects/statuses" },
            new { Name = "projects.difficulties", Route = "/api/metadata/projects/difficulties" },
            new { Name = "projects.visibilities", Route = "/api/metadata/projects/visibilities" }
        };

        var generatedAt = DateTime.UtcNow;
        _logger.LogDebug("Metadata index requested at {GeneratedAt}", generatedAt);

        return Ok(new { GeneratedAt = generatedAt, Sections = sections });
    }

    /// <summary>
    /// Returns distinct non-empty skill categories from the skills catalog.
    /// </summary>
    [HttpGet("skills/categories")]
    public async Task<IActionResult> GetSkillCategories(CancellationToken ct = default)
    {
        var categories = await _context.Skills
            .AsNoTracking()
            .Select(s => s.Category)
            .Where(c => c != null && c != "")
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return Ok(categories);
    }

    /// <summary>
    /// Returns distinct non-empty badge categories from achievements.
    /// </summary>
    [HttpGet("badges/categories")]
    public async Task<IActionResult> GetBadgeCategories(CancellationToken ct = default)
    {
        var categories = await _context.Achievements
            .AsNoTracking()
            .Select(a => a.Category)
            .Where(c => c != null && c != "")
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return Ok(categories);
    }

    /// <summary>
    /// Returns distinct non-empty project status values from existing projects.
    /// </summary>
    [HttpGet("projects/statuses")]
    public async Task<IActionResult> GetProjectStatuses(CancellationToken ct = default)
    {
        var statuses = await _context.Projects
            .AsNoTracking()
            .Select(p => p.Status)
            .Where(s => s != null && s != "")
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

        return Ok(statuses);
    }

    /// <summary>
    /// Returns distinct non-empty project difficulty levels from existing projects.
    /// </summary>
    [HttpGet("projects/difficulties")]
    public async Task<IActionResult> GetProjectDifficulties(CancellationToken ct = default)
    {
        var difficulties = await _context.Projects
            .AsNoTracking()
            .Select(p => p.DifficultyLevel)
            .Where(d => d != null && d != "")
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);

        return Ok(difficulties);
    }

    /// <summary>
    /// Returns distinct non-empty project visibility modes from existing projects.
    /// </summary>
    [HttpGet("projects/visibilities")]
    public async Task<IActionResult> GetProjectVisibilities(CancellationToken ct = default)
    {
        var visibilities = await _context.Projects
            .AsNoTracking()
            .Select(p => p.Visibility)
            .Where(v => v != null && v != "")
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync(ct);

        return Ok(visibilities);
    }
}
