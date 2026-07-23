using System;
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
/// Controller for managing skills and technologies.
/// </summary>
/// <remarks>
/// This controller implements CRUD operations for the Skills catalog and UserSkills management.
///
/// According to ERD (devhunt_erd.puml):
/// - Skills: Master catalog of technologies and skills (C#, React, PostgreSQL, etc.)
/// - UserSkills: Junction table linking users to skills with proficiency levels and experience
///
/// Core functionality:
/// - Public skills catalog with search and categorization
/// - User skill profiles with proficiency tracking (beginner → intermediate → advanced → expert)
/// - Admin-managed skill verification system
/// - Experience tracking in years
///
/// Routes: api/skills/*
/// </remarks>
[ApiController]
[Route("api/skills")]
[Authorize]
public class SkillsController : ControllerBase
{
    private readonly DevHuntDbContext _context;
    private readonly ILogger<SkillsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillsController"/> class.
    /// </summary>
    /// <param name="context">Database context used for catalog skills, aliases, user skills, and project tech stacks.</param>
    /// <param name="logger">Logger used to record skill catalog and user-skill changes.</param>
    public SkillsController(DevHuntDbContext context, ILogger<SkillsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Reads the authenticated user's identifier from claims and fails fast when authentication middleware did not provide it.
    /// </summary>
    /// <returns>The current user's ID.</returns>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Normalizes a skill name or alias token for case-insensitive and punctuation-tolerant matching.
    /// </summary>
    /// <param name="value">Raw skill name or alias token.</param>
    /// <returns>The canonical token used for alias lookup.</returns>
    private static string NormalizeSkillToken(string value) => SkillNormalization.NormalizeSkillToken(value);

    /// <summary>Suggested skill result for autocomplete.</summary>
    /// <param name="Id">Skill identifier.</param>
    /// <param name="Name">Skill name.</param>
    /// <param name="Category">Skill category.</param>
    /// <param name="Match">Matched token used for highlighting.</param>
    public sealed record SkillSuggestItemDto(Guid Id, string Name, string Category, string Match);

    /// <summary>Resolved skill item for a raw user token.</summary>
    /// <param name="Raw">Original raw token.</param>
    /// <param name="Id">Resolved skill identifier.</param>
    /// <param name="Name">Resolved skill name.</param>
    /// <param name="Category">Resolved skill category.</param>
    /// <param name="Match">Matched token or alias.</param>
    public sealed record SkillResolveItemDto(string Raw, Guid Id, string Name, string Category, string Match);

    #region Public Skills

    /// <summary>
    /// Retrieves all skills from the catalog with pagination and optional category filtering.
    /// </summary>
    /// <remarks>
    /// This endpoint returns the complete skills catalog, useful for:
    /// - Populating skill selection dropdowns
    /// - Browsing available technologies
    /// - Building skill-based search interfaces
    ///
    /// Results are sorted alphabetically by category, then by name within each category.
    ///
    /// Security:
    /// - Page size is capped at 200 to prevent excessive data transfer
    /// - Invalid pagination parameters are auto-corrected (not rejected)
    ///
    /// Accessible to anonymous users for public browsing.
    /// </remarks>
    /// <param name="category">Optional category filter (e.g., "Programming Languages", "Frameworks", "Databases").</param>
    /// <param name="page">Page number (minimum: 1, default: 1).</param>
    /// <param name="pageSize">Items per page (minimum: 1, maximum: 200, default: 50).</param>
    /// <param name="ct">Cancellation token for the catalog query.</param>
    /// <returns>Paginated list of skills with metadata.</returns>
    /// <response code="200">Returns the paginated skill list.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllSkills(
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        // SECURITY: Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200; // SECURITY: Limit max page size

        var query = _context.Skills.AsNoTracking();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(s => s.Category == category);
        }

        var totalCount = await query.CountAsync(ct);
        var skills = await query
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new
        {
            Items = skills,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            HasNext = page * pageSize < totalCount,
            HasPrevious = page > 1
        });
    }

    /// <summary>
    /// Retrieves a single skill by its unique identifier.
    /// </summary>
    /// <remarks>
    /// Returns the complete skill details including name, category, description, and icon URL.
    /// Accessible to anonymous users.
    /// </remarks>
    /// <param name="id">The unique identifier of the skill.</param>
    /// <param name="ct">Cancellation token for the catalog lookup.</param>
    /// <returns>The skill details.</returns>
    /// <response code="200">Returns the skill.</response>
    /// <response code="404">If the skill is not found.</response>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSkill(Guid id, CancellationToken ct = default)
    {
        var skill = await _context.Skills
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (skill == null)
        {
            return NotFound("Skill not found");
        }

        return Ok(skill);
    }

    /// <summary>
    /// Searches for skills by name (case-insensitive partial matching).
    /// </summary>
    /// <remarks>
    /// This endpoint is optimized for autocomplete and typeahead scenarios.
    ///
    /// Behavior:
    /// - Performs case-insensitive substring matching on skill names
    /// - Returns up to 20 results, sorted by category then name
    /// - Requires minimum 2 characters to prevent overly broad searches
    ///
    /// Example: searching for "java" will match "Java", "JavaScript", etc.
    ///
    /// Accessible to anonymous users.
    /// </remarks>
    /// <param name="query">Search term (minimum 2 characters).</param>
    /// <param name="ct">Cancellation token for the catalog search.</param>
    /// <returns>List of matching skills (max 20).</returns>
    /// <response code="200">Returns the matching skills.</response>
    /// <response code="400">If the query is too short (less than 2 characters).</response>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchSkills([FromQuery] string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return BadRequest("Query must be at least 2 characters");
        }

        var skills = await _context.Skills
            .AsNoTracking()
            .Where(s => s.Name.ToLower().Contains(query.ToLower()))
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .Take(20)
            .ToListAsync(ct);

        return Ok(skills);
    }

    /// <summary>
    /// Suggest skills for autocomplete with support for aliases/synonyms.
    /// </summary>
    /// <remarks>
    /// Unlike <c>/api/skills/search</c>, this endpoint is tolerant to common variants (e.g. "c#" / "csharp", ".net" / "dotnet").
    /// Intended for typeahead UI.
    /// </remarks>
    /// <param name="q">Search term (minimum 1 character).</param>
    /// <param name="category">Optional category filter (matches <see cref="Skill.Category"/> exactly).</param>
    /// <param name="limit">Max number of suggestions (1..20, default 10).</param>
    /// <param name="ct">Cancellation token for catalog and alias queries.</param>
    /// <returns>Ranked suggestions with match metadata, or 400 when the query is blank.</returns>
    [HttpGet("suggest")]
    [AllowAnonymous]
    public async Task<IActionResult> SuggestSkills(
        [FromQuery][Required] string q,
        [FromQuery] string? category = null,
        [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("q is required");
        }

        if (limit < 1) limit = 10;
        if (limit > 20) limit = 20;

        var qLower = q.Trim().ToLowerInvariant();
        var qNorm = NormalizeSkillToken(q);

        // Alias candidates (fast path)
        var aliasQuery = _context.SkillAliases
            .AsNoTracking()
            .Include(a => a.Skill)
            .Where(a => a.AliasNormalized.StartsWith(qNorm));

        if (!string.IsNullOrWhiteSpace(category))
        {
            aliasQuery = aliasQuery.Where(a => a.Skill.Category == category);
        }

        var aliasCandidates = await aliasQuery
            .Select(a => new
            {
                a.Skill.Id,
                a.Skill.Name,
                a.Skill.Category,
                a.AliasNormalized
            })
            .Take(60)
            .ToListAsync(ct);

        // Name candidates (fallback)
        var nameQuery = _context.Skills
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            nameQuery = nameQuery.Where(s => s.Category == category);
        }

        var nameCandidates = await nameQuery
            .Where(s => s.Name.ToLower().Contains(qLower))
            .Select(s => new { s.Id, s.Name, s.Category })
            .Take(60)
            .ToListAsync(ct);

        // Rank and merge
        var seen = new HashSet<Guid>();
        var ranked = new List<(Guid Id, string Name, string Category, int Rank, string Match)>();

        foreach (var a in aliasCandidates)
        {
            var matchType = a.AliasNormalized == qNorm ? "alias_exact" : "alias_prefix";
            var rank = a.AliasNormalized == qNorm ? 0 : 2;
            ranked.Add((a.Id, a.Name, a.Category, rank, matchType));
        }

        foreach (var s in nameCandidates)
        {
            var nameNorm = NormalizeSkillToken(s.Name);
            var matchType = "name_contains";
            var rank = 4;

            if (!string.IsNullOrEmpty(qNorm) && nameNorm == qNorm)
            {
                matchType = "name_exact";
                rank = 1;
            }
            else if (s.Name.ToLower().StartsWith(qLower))
            {
                matchType = "name_prefix";
                rank = 3;
            }

            ranked.Add((s.Id, s.Name, s.Category, rank, matchType));
        }

        var items = ranked
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Name.Length)
            .ThenBy(x => x.Category)
            .ThenBy(x => x.Name)
            .Where(x => seen.Add(x.Id))
            .Take(limit)
            .Select(x => new SkillSuggestItemDto(x.Id, x.Name, x.Category, x.Match))
            .ToList();

        return Ok(new { Items = items, Count = items.Count });
    }

    /// <summary>
    /// Retrieves all distinct skill categories.
    /// </summary>
    /// <remarks>
    /// Returns a unique, alphabetically sorted list of all categories present in the skills catalog.
    /// Useful for building category filter interfaces.
    ///
    /// Examples of categories: "Programming Languages", "Frameworks", "Databases", "DevOps Tools", etc.
    ///
    /// Accessible to anonymous users.
    /// </remarks>
    /// <returns>Alphabetically sorted list of category names.</returns>
    /// <response code="200">Returns the list of categories.</response>
    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories(CancellationToken ct = default)
    {
        var categories = await _context.Skills
            .AsNoTracking()
            .Select(s => s.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return Ok(categories);
    }

    /// <summary>
    /// Resolves free-form skill strings to canonical catalog skills (exact name or exact alias).
    /// </summary>
    /// <remarks>
    /// Intended for UI use when the profile stores skills as raw strings.
    /// Helps:
    /// - normalize inputs (e.g. "react" -> "React")
    /// - derive categories for grouping (e.g. "React" -> "Web")
    ///
    /// This endpoint only resolves exact matches (case-insensitive for names; normalized exact for aliases).
    /// </remarks>
    /// <param name="names">Repeated query param: ?name=react&amp;name=dotnet</param>
    /// <param name="ct">Cancellation token for catalog and alias queries.</param>
    /// <returns>Canonical skill matches for up to 100 non-blank distinct input names.</returns>
    [HttpGet("resolve")]
    [AllowAnonymous]
    public async Task<IActionResult> ResolveSkills([FromQuery(Name = "name")] string[] names, CancellationToken ct = default)
    {
        if (names == null || names.Length == 0)
        {
            return Ok(new { Items = Array.Empty<SkillResolveItemDto>(), Count = 0 });
        }

        var cleaned = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();

        if (cleaned.Count == 0)
        {
            return Ok(new { Items = Array.Empty<SkillResolveItemDto>(), Count = 0 });
        }

        var lowerInputs = cleaned
            .Select(n => n.ToLowerInvariant())
            .ToList();

        var normalizedInputs = cleaned
            .Select(NormalizeSkillToken)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Exact name matches (case-insensitive)
        var nameMatches = await _context.Skills
            .AsNoTracking()
            .Where(s => lowerInputs.Contains(s.Name.ToLower()))
            .Select(s => new { s.Id, s.Name, s.Category })
            .ToListAsync(ct);

        var byLowerName = nameMatches
            .GroupBy(s => s.Name.ToLowerInvariant())
            .Select(g => g.First())
            .ToDictionary(s => s.Name.ToLowerInvariant(), s => s);

        // Exact alias matches (normalized)
        var aliasMatches = await _context.SkillAliases
            .AsNoTracking()
            .Include(a => a.Skill)
            .Where(a => normalizedInputs.Contains(a.AliasNormalized))
            .Select(a => new { a.AliasNormalized, a.Skill.Id, a.Skill.Name, a.Skill.Category })
            .ToListAsync(ct);

        var byAliasNorm = aliasMatches
            .GroupBy(a => a.AliasNormalized)
            .Select(g => g.First())
            .ToDictionary(a => a.AliasNormalized, a => a);

        var items = new List<SkillResolveItemDto>(cleaned.Count);
        foreach (var raw in cleaned)
        {
            var rawLower = raw.ToLowerInvariant();
            if (byLowerName.TryGetValue(rawLower, out var skill))
            {
                items.Add(new SkillResolveItemDto(raw, skill.Id, skill.Name, skill.Category, "name_exact"));
                continue;
            }

            var rawNorm = NormalizeSkillToken(raw);
            if (!string.IsNullOrWhiteSpace(rawNorm) && byAliasNorm.TryGetValue(rawNorm, out var alias))
            {
                items.Add(new SkillResolveItemDto(raw, alias.Id, alias.Name, alias.Category, "alias_exact"));
            }
        }

        return Ok(new { Items = items, Count = items.Count });
    }

    #endregion

    #region Admin Skills Management

    /// <summary>
    /// Creates a new skill in the catalog.
    /// </summary>
    /// <remarks>
    /// This endpoint allows administrators and curators to expand the skills catalog.
    ///
    /// Validation:
    /// - Skill names must be unique (case-insensitive)
    /// - Name is required (max 100 characters)
    /// - Category is required (max 50 characters)
    /// - Description and IconUrl are optional
    ///
    /// Side effects:
    /// - Logs the creation event with user ID
    ///
    /// Requires admin or curator role.
    /// </remarks>
    /// <param name="dto">The skill creation data.</param>
    /// <param name="ct">Cancellation token for persistence.</param>
    /// <returns>The created skill with its ID.</returns>
    /// <response code="201">Returns the created skill with location header.</response>
    /// <response code="409">If a skill with this name already exists.</response>
    /// <response code="403">If the user is not an admin or curator.</response>
    [HttpPost]
    [Authorize(Policy = "AdminOrCurator")]
    public async Task<IActionResult> CreateSkill([FromBody] CreateSkillDto dto, CancellationToken ct = default)
    {
        // B-10: validate IconUrl before persisting
        if (!string.IsNullOrEmpty(dto.IconUrl) && !SecurityHelpers.IsValidUrl(dto.IconUrl))
            return BadRequest("Invalid icon URL.");

        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Category = dto.Category,
            Description = dto.Description,
            IconUrl = dto.IconUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Skills.Add(skill);
        // B-08: Rely on IX_Skills_Name unique index instead of TOCTOU AnyAsync check
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict("Skill with this name already exists");
        }

        _logger.LogInformation("Skill {SkillId} created by {UserId}", skill.Id, GetRequiredUserId());

        return CreatedAtAction(nameof(GetSkill), new { id = skill.Id }, skill);
    }

    /// <summary>
    /// Updates an existing skill in the catalog.
    /// </summary>
    /// <remarks>
    /// Allows modification of all skill properties (name, category, description, icon).
    ///
    /// Validation:
    /// - If the name is changed, it must remain unique (case-insensitive)
    /// - All fields follow the same constraints as CreateSkill
    ///
    /// Side effects:
    /// - Logs the update event with user ID
    ///
    /// Note: Updating a skill does not affect existing UserSkills or ProjectTechStacks relationships.
    ///
    /// Requires admin or curator role.
    /// </remarks>
    /// <param name="id">The unique identifier of the skill to update.</param>
    /// <param name="dto">The updated skill data.</param>
    /// <param name="ct">Cancellation token for persistence.</param>
    /// <returns>The updated skill.</returns>
    /// <response code="200">Returns the updated skill.</response>
    /// <response code="404">If the skill is not found.</response>
    /// <response code="409">If the new name conflicts with an existing skill.</response>
    /// <response code="403">If the user is not an admin or curator.</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOrCurator")]
    public async Task<IActionResult> UpdateSkill(Guid id, [FromBody] UpdateSkillDto dto, CancellationToken ct = default)
    {
        var skill = await _context.Skills.FindAsync(new object[] { id }, ct);
        if (skill == null)
        {
            return NotFound("Skill not found");
        }

        // B-10: validate IconUrl before persisting
        if (!string.IsNullOrEmpty(dto.IconUrl) && !SecurityHelpers.IsValidUrl(dto.IconUrl))
            return BadRequest("Invalid icon URL.");

        skill.Name = dto.Name;
        skill.Category = dto.Category;
        skill.Description = dto.Description;
        skill.IconUrl = dto.IconUrl;

        // B-08: Rely on IX_Skills_Name unique index instead of TOCTOU AnyAsync check
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict("Skill with this name already exists");
        }

        _logger.LogInformation("Skill {SkillId} updated by {UserId}", id, GetRequiredUserId());

        return Ok(skill);
    }

    /// <summary>
    /// Deletes a skill from the catalog.
    /// </summary>
    /// <remarks>
    /// This endpoint permanently removes a skill, but only if it's not in use.
    ///
    /// Referential integrity checks:
    /// - Prevents deletion if any users have this skill in their profile (UserSkills)
    /// - Prevents deletion if any projects use this skill (ProjectTechStacks)
    ///
    /// This protects data consistency and prevents orphaned relationships.
    /// If deletion is needed despite usage, consider:
    /// 1. Marking the skill as deprecated (add a flag)
    /// 2. Migrating users/projects to a different skill first
    ///
    /// Side effects:
    /// - Logs the deletion event with user ID
    ///
    /// Requires admin or curator role.
    /// </remarks>
    /// <param name="id">The unique identifier of the skill to delete.</param>
    /// <param name="ct">Cancellation token for usage checks and persistence.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">If the skill was successfully deleted.</response>
    /// <response code="400">If the skill is being used by users or projects.</response>
    /// <response code="404">If the skill is not found.</response>
    /// <response code="403">If the user is not an admin or curator.</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOrCurator")]
    public async Task<IActionResult> DeleteSkill(Guid id, CancellationToken ct = default)
    {
        var skill = await _context.Skills.FindAsync(new object[] { id }, ct);
        if (skill == null)
        {
            return NotFound("Skill not found");
        }

        // Check if skill is being used
        var hasUsers = await _context.UserSkills.AnyAsync(us => us.SkillId == id, ct);
        var hasProjects = await _context.ProjectTechStacks.AnyAsync(pts => pts.SkillId == id, ct);

        if (hasUsers || hasProjects)
        {
            return BadRequest("Cannot delete skill: it is being used by users or projects");
        }

        _context.Skills.Remove(skill);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Skill {SkillId} deleted by {UserId}", id, GetRequiredUserId());

        return NoContent();
    }

    #endregion

    #region User Skills Management

    /// <summary>
    /// Retrieves the authenticated user's skill profile.
    /// </summary>
    /// <remarks>
    /// Returns all skills that the user has added to their profile, including:
    /// - Skill details (name, category, description, icon)
    /// - Proficiency level (beginner, intermediate, advanced, expert)
    /// - Years of experience
    /// - Verification status (verified by admin/curator or not)
    ///
    /// Results are sorted by category, then by skill name.
    ///
    /// Note: Although user skill lists are typically small, pagination is included for API consistency
    /// and to support users with extensive skill profiles.
    /// </remarks>
    /// <param name="page">Page number (minimum: 1, default: 1).</param>
    /// <param name="pageSize">Items per page (minimum: 1, maximum: 200, default: 100).</param>
    /// <param name="ct">Cancellation token for the user-skill query.</param>
    /// <returns>Paginated list of the user's skills.</returns>
    /// <response code="200">Returns the user's skill profile.</response>
    [HttpGet("my")]
    public async Task<IActionResult> GetMySkills([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        // Validate pagination
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 100;
        if (pageSize > 200) pageSize = 200;

        var query = _context.UserSkills
            .Include(us => us.Skill)
            .Where(us => us.UserId == userId);

        var totalCount = await query.CountAsync(ct);
        var userSkills = await query
            .OrderBy(us => us.Skill.Category)
            .ThenBy(us => us.Skill.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(us => new
            {
                us.Id,
                Skill = new
                {
                    us.Skill.Id,
                    us.Skill.Name,
                    us.Skill.Category,
                    us.Skill.Description,
                    us.Skill.IconUrl
                },
                us.ProficiencyLevel,
                us.YearsOfExperience,
                us.Verified,
                us.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            Items = userSkills,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            HasNext = page * pageSize < totalCount,
            HasPrevious = page > 1
        });
    }

    /// <summary>
    /// Adds a skill to the authenticated user's profile.
    /// </summary>
    /// <remarks>
    /// This endpoint allows users to build their skill profiles for:
    /// - Matching with relevant projects
    /// - Showcasing expertise to potential collaborators
    /// - Powering the ML recommendation engine
    ///
    /// Validation:
    /// - The skill must exist in the catalog
    /// - Users cannot add the same skill twice (duplicate prevention)
    /// - Proficiency level must be: beginner, intermediate, advanced, or expert
    /// - Years of experience is optional
    ///
    /// New skills are initially unverified (Verified=false).
    /// Admins/curators can verify skills later to indicate credibility.
    ///
    /// Side effects:
    /// - Logs the skill addition event
    /// </remarks>
    /// <param name="skillId">The unique identifier of the skill to add.</param>
    /// <param name="dto">The skill proficiency data.</param>
    /// <param name="ct">Cancellation token for catalog lookup, duplicate check, and persistence.</param>
    /// <returns>The created user skill record.</returns>
    /// <response code="201">Returns the created user skill.</response>
    /// <response code="404">If the skill is not found in the catalog.</response>
    /// <response code="409">If the user already has this skill.</response>
    [HttpPost("my/{skillId}")]
    public async Task<IActionResult> AddSkillToMe(Guid skillId, [FromBody] AddSkillDto dto, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        // Check if skill exists
        var skill = await _context.Skills.FindAsync(new object[] { skillId }, ct);
        if (skill == null)
        {
            return NotFound("Skill not found");
        }

        // Check that user hasn't added this skill yet
        var exists = await _context.UserSkills.AnyAsync(us => us.UserId == userId && us.SkillId == skillId, ct);
        if (exists)
        {
            return Conflict("You already have this skill");
        }

        var userSkill = new UserSkill
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SkillId = skillId,
            ProficiencyLevel = dto.ProficiencyLevel,
            YearsOfExperience = dto.YearsOfExperience,
            Verified = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserSkills.Add(userSkill);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} added skill {SkillId}", userId, skillId);

        return CreatedAtAction(nameof(GetMySkills), userSkill);
    }

    /// <summary>
    /// Updates proficiency level or experience for a skill in the user's profile.
    /// </summary>
    /// <remarks>
    /// Allows users to update their skill expertise as they gain experience.
    ///
    /// Common use cases:
    /// - Advancing proficiency level as skills improve (e.g., intermediate → advanced)
    /// - Updating years of experience annually
    ///
    /// Note: This does not reset the Verified flag - verified skills remain verified.
    ///
    /// Side effects:
    /// - Logs the skill update event
    /// </remarks>
    /// <param name="skillId">The unique identifier of the skill to update.</param>
    /// <param name="dto">The updated proficiency and experience data.</param>
    /// <param name="ct">Cancellation token for lookup and persistence.</param>
    /// <returns>The updated user skill record.</returns>
    /// <response code="200">Returns the updated user skill.</response>
    /// <response code="404">If the skill is not found in the user's profile.</response>
    [HttpPut("my/{skillId}")]
    public async Task<IActionResult> UpdateMySkill(Guid skillId, [FromBody] UpdateUserSkillDto dto, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var userSkill = await _context.UserSkills
            .FirstOrDefaultAsync(us => us.UserId == userId && us.SkillId == skillId, ct);

        if (userSkill == null)
        {
            return NotFound("Skill not found in your profile");
        }

        userSkill.ProficiencyLevel = dto.ProficiencyLevel;
        userSkill.YearsOfExperience = dto.YearsOfExperience;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} updated skill {SkillId}", userId, skillId);

        return Ok(userSkill);
    }

    /// <summary>
    /// Removes a skill from the authenticated user's profile.
    /// </summary>
    /// <remarks>
    /// This permanently deletes the user-skill relationship.
    ///
    /// Common use cases:
    /// - Removing outdated or no-longer-relevant skills
    /// - Cleaning up incorrect entries
    ///
    /// Side effects:
    /// - Logs the skill removal event
    /// - May affect ML recommendation accuracy until recalculated
    /// </remarks>
    /// <param name="skillId">The unique identifier of the skill to remove.</param>
    /// <param name="ct">Cancellation token for lookup and persistence.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">If the skill was successfully removed.</response>
    /// <response code="404">If the skill is not found in the user's profile.</response>
    [HttpDelete("my/{skillId}")]
    public async Task<IActionResult> RemoveSkillFromMe(Guid skillId, CancellationToken ct = default)
    {
        var userId = GetRequiredUserId();

        var userSkill = await _context.UserSkills
            .FirstOrDefaultAsync(us => us.UserId == userId && us.SkillId == skillId, ct);

        if (userSkill == null)
        {
            return NotFound("Skill not found in your profile");
        }

        _context.UserSkills.Remove(userSkill);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} removed skill {SkillId}", userId, skillId);

        return NoContent();
    }

    /// <summary>
    /// Retrieves the skill profile of a specific user (public view).
    /// </summary>
    /// <remarks>
    /// This endpoint provides public access to user skill profiles for:
    /// - Viewing potential collaborator expertise
    /// - Evaluating project team member capabilities
    /// - Building user profile pages
    ///
    /// Privacy considerations:
    /// - CreatedAt timestamp is hidden from public view
    /// - Only includes: skill name, category, icon, proficiency, experience, and verification status
    ///
    /// Results are sorted by category, then by skill name.
    /// Accessible to anonymous users.
    /// </remarks>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="page">Page number (minimum: 1, default: 1).</param>
    /// <param name="pageSize">Items per page (minimum: 1, maximum: 200, default: 100).</param>
    /// <param name="ct">Cancellation token for the public skill query.</param>
    /// <returns>Paginated list of the user's public skills.</returns>
    /// <response code="200">Returns the user's public skill profile.</response>
    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserSkills(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
    {
        // Validate pagination
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 100;
        if (pageSize > 200) pageSize = 200;

        var query = _context.UserSkills
            .Include(us => us.Skill)
            .Where(us => us.UserId == userId);

        var totalCount = await query.CountAsync(ct);
        var userSkills = await query
            .OrderBy(us => us.Skill.Category)
            .ThenBy(us => us.Skill.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(us => new
            {
                Skill = new
                {
                    us.Skill.Id,
                    us.Skill.Name,
                    us.Skill.Category,
                    us.Skill.IconUrl
                },
                us.ProficiencyLevel,
                us.YearsOfExperience,
                us.Verified,
                // Hide created_at from public
            })
            .ToListAsync(ct);

        return Ok(new
        {
            Items = userSkills,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            HasNext = page * pageSize < totalCount,
            HasPrevious = page > 1
        });
    }

    /// <summary>
    /// Verifies a user's claimed skill (admin/curator endorsement).
    /// </summary>
    /// <remarks>
    /// This endpoint allows trusted administrators and curators to verify user skills,
    /// adding credibility to user profiles.
    ///
    /// Verification use cases:
    /// - Confirming skills based on portfolio review
    /// - Endorsing skills after code review or assessment
    /// - Validating claims based on project contributions
    ///
    /// Verified skills may:
    /// - Display a verification badge in the UI
    /// - Carry more weight in ML recommendations
    /// - Increase user trustworthiness in the community
    ///
    /// Side effects:
    /// - Logs the verification event with verifier's user ID
    ///
    /// Requires admin or curator role.
    /// </remarks>
    /// <param name="skillId">The unique identifier of the skill.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">Cancellation token for lookup and persistence.</param>
    /// <returns>The updated user skill record with Verified=true.</returns>
    /// <response code="200">Returns the verified user skill.</response>
    /// <response code="404">If the user skill is not found.</response>
    /// <response code="403">If the user is not an admin or curator.</response>
    [HttpPut("{skillId}/verify/{userId}")]
    [Authorize(Policy = "AdminOrCurator")]
    public async Task<IActionResult> VerifyUserSkill(Guid skillId, Guid userId, CancellationToken ct = default)
    {
        var userSkill = await _context.UserSkills
            .FirstOrDefaultAsync(us => us.UserId == userId && us.SkillId == skillId, ct);

        if (userSkill == null)
        {
            return NotFound("User skill not found");
        }

        userSkill.Verified = true;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} skill {SkillId} verified by {AdminId}", userId, skillId, GetRequiredUserId());

        return Ok(userSkill);
    }

    #endregion
}

/// <summary>
/// Data transfer object for creating a new skill in the catalog.
/// </summary>
public class CreateSkillDto
{
    /// <summary>Skill name.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Skill category.</summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional icon URL.</summary>
    public string? IconUrl { get; set; }
}

/// <summary>
/// Data transfer object for updating an existing skill in the catalog.
/// </summary>
public class UpdateSkillDto
{
    /// <summary>Updated skill name.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Updated skill category.</summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Updated description.</summary>
    public string? Description { get; set; }

    /// <summary>Updated icon URL.</summary>
    public string? IconUrl { get; set; }
}

/// <summary>
/// Data transfer object for adding a skill to a user's profile.
/// </summary>
/// <remarks>
/// Proficiency levels: beginner, intermediate, advanced, expert
/// </remarks>
public class AddSkillDto
{
    /// <summary>Proficiency level (beginner, intermediate, advanced, expert).</summary>
    [Required]
    [MaxLength(50)]
    public string ProficiencyLevel { get; set; } = "intermediate";  // beginner, intermediate, advanced, expert

    /// <summary>Years of experience (optional).</summary>
    public int? YearsOfExperience { get; set; }
}

/// <summary>
/// Data transfer object for updating a user's skill proficiency and experience.
/// </summary>
public class UpdateUserSkillDto
{
    /// <summary>Updated proficiency level.</summary>
    [Required]
    [MaxLength(50)]
    public string ProficiencyLevel { get; set; } = "intermediate";

    /// <summary>Updated years of experience.</summary>
    public int? YearsOfExperience { get; set; }
}

