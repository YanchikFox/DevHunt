using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Models;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Moderation;
using DevHunt.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Moderation endpoints for reporting and processing abuse reports.
/// </summary>
[ApiController]
[Route("api/moderation")]
[Authorize]
public class ModerationController : ControllerBase
{
    private readonly IModerationService _moderation;
    private readonly DevHuntDbContext _db;

    /// <summary>
    /// Creates the moderation controller with report-processing and database-backed role validation services.
    /// </summary>
    /// <param name="moderation">Moderation service that handles report submission and decisions.</param>
    /// <param name="db">Database context used to verify active admin and curator roles.</param>
    public ModerationController(IModerationService moderation, DevHuntDbContext db)
    {
        _moderation = moderation;
        _db = db;
    }

    /// <summary>
    /// Submits a moderation report for the authenticated user and maps service result codes to HTTP responses.
    /// </summary>
    [HttpPost("report")]
    public async Task<IActionResult> Report([FromBody] SubmitReportRequest request, CancellationToken ct)
    {
        var userId = GetRequiredUserId();
        var result = await _moderation.SubmitReportAsync(userId, request, ct);

        return result.StatusCode switch
        {
            200 => Ok(result.Data),
            400 => BadRequest(result.Error),
            409 => Conflict(result.Error),
            _ => StatusCode(result.StatusCode, result.Error)
        };
    }

    /// <summary>
    /// Lists pending moderation reports for active admins and curators.
    /// </summary>
    [HttpGet("queue")]
    public async Task<IActionResult> Queue(CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync(ct))
            return Forbid();

        var result = await _moderation.GetQueueAsync(ct);
        return Ok(result.Data);
    }

    /// <summary>
    /// Processes a moderation decision as the current active admin or curator and maps service result codes to HTTP responses.
    /// </summary>
    [HttpPost("decision")]
    public async Task<IActionResult> Decision([FromBody] DecisionRequest request, CancellationToken ct)
    {
        if (!await IsAdminOrCuratorAsync(ct))
            return Forbid();

        var moderatorId = GetRequiredUserId();
        var result = await _moderation.ProcessDecisionAsync(moderatorId, request, ct);

        return result.StatusCode switch
        {
            200 => Ok(result.Data),
            404 => NotFound(result.Error),
            _ => StatusCode(result.StatusCode, result.Error)
        };
    }

    // AP-01: DB-validated role check — protects against demoted admin with valid JWT
    /// <summary>
    /// Checks the database for an active admin, curator, or superadmin matching the current claims principal.
    /// </summary>
    private async Task<bool> IsAdminOrCuratorAsync(CancellationToken ct = default)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return false;
        var role = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId.Value && u.IsActive)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);
        return role is UserRoles.Admin or UserRoles.Curator or UserRoles.SuperAdmin;
    }

    /// <summary>
    /// Returns the authenticated user's identifier or throws when the JWT is missing the user claim.
    /// </summary>
    private Guid GetRequiredUserId()
    {
        return SecurityHelpers.GetUserId(User)
            ?? throw new InvalidOperationException("User identifier claim is missing");
    }
}
