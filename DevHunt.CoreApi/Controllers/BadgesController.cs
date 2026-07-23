using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Services.Badges;
using DevHunt.CoreApi.Security;
using DevHunt.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Canonical badges controller. Public API: /api/badges.
/// </summary>
[ApiController]
[Route("api/badges")]
public class BadgesController : ControllerBase
{
    private readonly IBadgesService _badgesService;
    private readonly DevHuntDbContext _db;
    private readonly ILogger<BadgesController> _logger;

    /// <summary>
    /// Creates the badges controller with badge workflow, persistence, and logging services.
    /// </summary>
    /// <param name="badgesService">Service that handles badge listing, awarding, progress, and definition changes.</param>
    /// <param name="db">Database context used for database-backed admin authorization.</param>
    /// <param name="logger">Logger reserved for badge workflow diagnostics.</param>
    public BadgesController(
        IBadgesService badgesService,
        DevHuntDbContext db,
        ILogger<BadgesController> logger)
    {
        _badgesService = badgesService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User) ?? throw new InvalidOperationException("User identifier claim is missing");
    }

    /// <summary>
    /// Checks the database for an active admin or superadmin instead of trusting JWT role claims.
    /// </summary>
    private async Task<bool> IsAdminAsync(CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return false;
        var role = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId.Value && u.IsActive)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);
        return role is UserRoles.Admin or UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Converts a typed badge service result into an HTTP response preserving service status codes.
    /// </summary>
    private IActionResult MapResult<T>(BadgeResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Converts a non-payload badge service result into no-content success or an error status code.
    /// </summary>
    private IActionResult MapResult(BadgeResult result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Lists badge definitions with optional category filtering and pagination.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetBadges(
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _badgesService.GetAllBadgesAsync(category, page, pageSize);
        return MapResult(result);
    }

    /// <summary>
    /// Returns one badge definition by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBadge(Guid id)
    {
        var result = await _badgesService.GetBadgeAsync(id);
        return MapResult(result);
    }

    /// <summary>
    /// Returns badges earned by the current user, including progress.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyBadges(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _badgesService.GetUserBadgesAsync(GetRequiredUserId(), page, pageSize, includeProgress: true);
        return MapResult(result);
    }

    /// <summary>
    /// Returns public badges for a specific user without progress details.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserBadges(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _badgesService.GetUserBadgesAsync(userId, page, pageSize, includeProgress: false);
        return MapResult(result);
    }

    /// <summary>
    /// Awards a badge or progress to a user when the caller is an active admin or superadmin.
    /// </summary>
    [HttpPost("award/{userId:guid}/{achievementCode}")]
    [Authorize]
    public async Task<IActionResult> AwardBadge(
        Guid userId,
        string achievementCode,
        [FromQuery] int? progress = null)
    {
        if (!await IsAdminAsync()) return Forbid();

        var request = new AwardBadgeRequest
        {
            UserId = userId,
            AchievementCode = achievementCode,
            Progress = progress,
            CurrentUserId = GetRequiredUserId()
        };
        var result = await _badgesService.AwardBadgeAsync(request);
        return MapResult(result);
    }

    /// <summary>
    /// Updates badge progress for a user, passing the caller's admin status to the badge service.
    /// </summary>
    [HttpPut("progress/{userId:guid}/{achievementCode}")]
    [Authorize]
    public async Task<IActionResult> UpdateProgress(Guid userId, string achievementCode, [FromBody] UpdateProgressDto dto)
    {
        var request = new UpdateBadgeProgressRequest
        {
            UserId = userId,
            AchievementCode = achievementCode,
            Progress = dto.Progress,
            CurrentUserId = GetRequiredUserId(),
            IsAdmin = SecurityHelpers.IsAdmin(User)
        };
        var result = await _badgesService.UpdateProgressAsync(request);
        return MapResult(result);
    }

    /// <summary>
    /// Returns aggregate badge statistics for a user.
    /// </summary>
    [HttpGet("stats/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUserStats(Guid userId)
    {
        var result = await _badgesService.GetUserStatsAsync(userId);
        return MapResult(result);
    }

    /// <summary>
    /// Creates a badge definition when the caller is an active admin or superadmin.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateBadge([FromBody] CreateAchievementDto dto)
    {
        if (!await IsAdminAsync()) return Forbid();

        // CreateBadgeAsync returns a typed BadgeResult<BadgeCreatedResponse>, so no dynamic cast is needed.
        var result = await _badgesService.CreateBadgeAsync(dto);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetBadge), new { id = result.Data!.Id }, result.Data);
        return StatusCode(result.StatusCode, result.ErrorMessage);
    }

    /// <summary>
    /// Updates a badge definition when the caller is an active admin or superadmin.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateBadge(Guid id, [FromBody] UpdateAchievementDto dto)
    {
        if (!await IsAdminAsync()) return Forbid();

        var result = await _badgesService.UpdateBadgeAsync(id, dto);
        return MapResult(result);
    }

    /// <summary>
    /// Deletes a badge definition when the caller is an active admin or superadmin.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteBadge(Guid id)
    {
        if (!await IsAdminAsync()) return Forbid();

        var result = await _badgesService.DeleteBadgeAsync(id);
        return MapResult(result);
    }
}
