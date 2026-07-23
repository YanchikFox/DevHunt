using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using DevHunt.CoreApi.Security;
using Npgsql;

namespace DevHunt.CoreApi.Services.Badges;

/// <summary>
/// CRUD and award operations for achievements/badges.
/// Used by admin endpoints and <see cref="AchievementTriggerService"/> for automated awards.
/// Sends in-app notifications after successful grants.
/// </summary>
public class BadgesService : IBadgesService
{
    private readonly DevHuntDbContext _context;
    private readonly INotificationHelperService _notifications;
    private readonly ILogger<BadgesService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BadgesService"/> class.
    /// </summary>
    /// <param name="context">Database context for achievements and user awards.</param>
    /// <param name="notifications">Notifier used after successful badge grants.</param>
    /// <param name="logger">Logger for award and admin operations.</param>
    public BadgesService(
        DevHuntDbContext context,
        INotificationHelperService notifications,
        ILogger<BadgesService> logger)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Clamps page to at least 1 and page size to 1..200.</summary>
    private void NormalizePagination(ref int page, ref int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 200) pageSize = 200;
    }

    /// <summary>Wraps list results with standard pagination metadata.</summary>
    private BadgeResult<object> CreatePagedResult<T>(List<T> items, int totalCount, int page, int pageSize)
    {
        return BadgeResult<object>.Success(new
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            HasNext = page * pageSize < totalCount,
            HasPrevious = page > 1
        });
    }

    /// <inheritdoc />
    public async Task<BadgeResult<object>> GetAllBadgesAsync(string? category, int page, int pageSize, CancellationToken ct = default)
    {
        NormalizePagination(ref page, ref pageSize);

        var query = _context.Achievements.AsNoTracking();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(a => a.Category == category);
        }

        var totalCount = await query.CountAsync(ct);
        var achievements = await query
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Points)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Code,
                a.Title,
                a.Description,
                a.IconUrl,
                a.Category,
                a.Points
            })
            .ToListAsync(ct);

        return CreatePagedResult(achievements, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<BadgeResult<object>> GetBadgeAsync(Guid id, CancellationToken ct = default)
    {
        var achievement = await _context.Achievements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (achievement == null) return BadgeResult<object>.Failure("Achievement not found", 404);

        return BadgeResult<object>.Success(new
        {
            achievement.Id,
            achievement.Code,
            achievement.Title,
            achievement.Description,
            achievement.IconUrl,
            achievement.Category,
            achievement.Points
        });
    }

    /// <inheritdoc />
    public async Task<BadgeResult<object>> GetUserBadgesAsync(Guid userId, int page, int pageSize, bool includeProgress, CancellationToken ct = default)
    {
        NormalizePagination(ref page, ref pageSize);

        var query = _context.UserAchievements
            .AsNoTracking()
            .Where(ua => ua.UserId == userId);

        var totalCount = await query.CountAsync(ct);
        var data = await query
            .OrderByDescending(ua => ua.EarnedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ua => new
            {
                Achievement = new
                {
                    ua.Achievement.Id,
                    ua.Achievement.Code,
                    ua.Achievement.Title,
                    ua.Achievement.Description,
                    ua.Achievement.IconUrl,
                    ua.Achievement.Category,
                    ua.Achievement.Points
                },
                ua.EarnedAt,
                Progress = includeProgress ? ua.Progress : (int?)null
            })
            .ToListAsync(ct);

        return CreatePagedResult(data, totalCount, page, pageSize);
    }

    // Rely on the unique user-achievement index instead of a pre-insert existence check.
    // Send the notification only after the database write succeeds.
    /// <inheritdoc />
    public async Task<BadgeResult<object>> AwardBadgeAsync(AwardBadgeRequest request, CancellationToken ct = default)
    {
        var achievement = await _context.Achievements.FirstOrDefaultAsync(a => a.Code == request.AchievementCode, ct);
        if (achievement == null) return BadgeResult<object>.Failure($"Achievement with code '{request.AchievementCode}' not found", 404);

        var user = await _context.Users.FindAsync(new object[] { request.UserId }, ct);
        if (user == null) return BadgeResult<object>.Failure("User not found", 404);

        var userAchievement = new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            AchievementId = achievement.Id,
            EarnedAt = DateTime.UtcNow,
            Progress = request.Progress
        };

        _context.UserAchievements.Add(userAchievement);

        try
        {
            // Save first so the unique index can reject duplicate awards.
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return BadgeResult<object>.Failure("User already has this achievement", 409);
        }

        // Notify only after the database write has been committed.
        await SendNotificationAsync(new BadgeNotification(request.UserId, achievement.Title, achievement.Description, achievement.Id), ct);

        _logger.LogInformation("Achievement {AchievementCode} awarded to user {UserId} by {CurrentUserId}",
            request.AchievementCode, request.UserId, request.CurrentUserId);

        return BadgeResult<object>.Success(new { UserAchievementId = userAchievement.Id, EarnedAt = userAchievement.EarnedAt });
    }

    // Only admin and superadmin users can manually change progress; regular users must use trigger-driven updates.
    /// <inheritdoc />
    public async Task<BadgeResult<object>> UpdateProgressAsync(UpdateBadgeProgressRequest request, CancellationToken ct = default)
    {
        if (!await CanUpdateProgressAsync(request.CurrentUserId, request.IsAdmin))
        {
            return BadgeResult<object>.Failure("Insufficient permissions to update progress", 403);
        }

        var achievement = await _context.Achievements.FirstOrDefaultAsync(a => a.Code == request.AchievementCode, ct);
        if (achievement == null) return BadgeResult<object>.Failure($"Achievement with code '{request.AchievementCode}' not found", 404);

        var userAchievement = await _context.UserAchievements
            .FirstOrDefaultAsync(ua => ua.UserId == request.UserId && ua.AchievementId == achievement.Id, ct);

        if (userAchievement == null) return BadgeResult<object>.Failure("User achievement not found", 404);

        userAchievement.Progress = Math.Clamp(request.Progress, 0, 100);
        await _context.SaveChangesAsync(ct);

        if (userAchievement.Progress == 100)
        {
            await SendNotificationAsync(new BadgeNotification(request.UserId, achievement.Title, achievement.Description, achievement.Id, IsNew: false), ct);
        }

        return BadgeResult<object>.Success(new { Progress = userAchievement.Progress });
    }

    // Check both admin and superadmin roles from the database before allowing manual progress updates.
    /// <summary>
    /// Allows manual progress updates only when the caller has an admin claim and an active admin or superadmin role in the database.
    /// </summary>
    private async Task<bool> CanUpdateProgressAsync(Guid currentUserId, bool isAdminClaim, CancellationToken ct = default)
    {
        if (!isAdminClaim) return false;

        var adminRole = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUserId && u.IsActive)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        if (adminRole is not ("admin" or "superadmin"))
        {
            _logger.LogWarning("User {UserId} attempted to use admin privileges without admin role in DB", currentUserId);
            return false;
        }

        return true;
    }

    // Use one grouped query instead of separate round trips for each aggregate.
    // TotalAchievements and TotalPoints are calculated from the loaded grouped result.
    /// <inheritdoc />
    public async Task<BadgeResult<object>> GetUserStatsAsync(Guid userId, CancellationToken ct = default)
    {
        var stats = await _context.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Include(ua => ua.Achievement)
            .GroupBy(ua => ua.Achievement.Category)
            .Select(g => new
            {
                Category = g.Key ?? "uncategorized",
                Count = g.Count(),
                TotalPoints = g.Sum(ua => ua.Achievement.Points)
            })
            .ToListAsync(ct);

        // Aggregate from the loaded grouped result.
        var totalAchievements = stats.Sum(s => s.Count);
        var totalPoints = stats.Sum(s => s.TotalPoints);

        return BadgeResult<object>.Success(new
        {
            TotalAchievements = totalAchievements,
            TotalPoints = totalPoints,
            ByCategory = stats
        });
    }

    // Rely on the unique Code index instead of a pre-insert existence check.
    // Validate IconUrl before saving and return a typed BadgeCreatedResponse.
    /// <inheritdoc />
    public async Task<BadgeResult<BadgeCreatedResponse>> CreateBadgeAsync(CreateAchievementDto dto, CancellationToken ct = default)
    {
        var achievement = new Achievement
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Title = dto.Title,
            Description = dto.Description,
            IconUrl = SecurityHelpers.IsValidUrl(dto.IconUrl) ? dto.IconUrl : null,
            Category = dto.Category,
            Points = dto.Points
        };

        _context.Achievements.Add(achievement);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return BadgeResult<BadgeCreatedResponse>.Failure($"Achievement with code '{dto.Code}' already exists", 409);
        }

        return BadgeResult<BadgeCreatedResponse>.Success(
            new BadgeCreatedResponse(achievement.Id, achievement.Code, achievement.Title,
                achievement.Description, achievement.IconUrl, achievement.Category, achievement.Points));
    }

    /// <inheritdoc />
    public async Task<BadgeResult<object>> UpdateBadgeAsync(Guid id, UpdateAchievementDto dto, CancellationToken ct = default)
    {
        var achievement = await _context.Achievements.FindAsync(new object[] { id }, ct);
        if (achievement == null) return BadgeResult<object>.Failure("Achievement not found", 404);

        if (!string.IsNullOrEmpty(dto.Code) && dto.Code != achievement.Code)
        {
            if (await _context.Achievements.AnyAsync(a => a.Code == dto.Code && a.Id != id, ct))
                return BadgeResult<object>.Failure($"Achievement with code '{dto.Code}' already exists", 409);
            achievement.Code = dto.Code;
        }

        UpdateAchievementFields(achievement, dto);
        await _context.SaveChangesAsync(ct);

        return BadgeResult<object>.Success(achievement);
    }

    // Validate IconUrl before assigning it to the achievement.
    /// <summary>Applies partial updates and validates icon URLs before assignment.</summary>
    private void UpdateAchievementFields(Achievement achievement, UpdateAchievementDto dto)
    {
        if (!string.IsNullOrEmpty(dto.Title)) achievement.Title = dto.Title;
        if (dto.Description != null) achievement.Description = dto.Description;
        if (dto.IconUrl != null) achievement.IconUrl = SecurityHelpers.IsValidUrl(dto.IconUrl) ? dto.IconUrl : null;
        if (dto.Category != null) achievement.Category = dto.Category;
        if (dto.Points.HasValue) achievement.Points = dto.Points.Value;
    }

    /// <inheritdoc />
    public async Task<BadgeResult> DeleteBadgeAsync(Guid id, CancellationToken ct = default)
    {
        var achievement = await _context.Achievements.FindAsync(new object[] { id }, ct);
        if (achievement == null) return BadgeResult.Failure("Achievement not found", 404);

        if (await _context.UserAchievements.AnyAsync(ua => ua.AchievementId == id, ct))
            return BadgeResult.Failure("Cannot delete achievement that has been awarded to users", 409);

        _context.Achievements.Remove(achievement);
        await _context.SaveChangesAsync(ct);

        return BadgeResult.Success();
    }

    // PostgreSQL unique-constraint violation check (SQLSTATE 23505).
    /// <summary>Detects PostgreSQL unique-violation errors (SQLSTATE 23505).</summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";

    /// <summary>
    /// Carries the user, achievement, and milestone state used to send an achievement notification.
    /// </summary>
    private record BadgeNotification(
        Guid UserId,
        string Title,
        string? Content,
        Guid AchievementId,
        bool IsNew = true);

    /// <summary>Sends an achievement notification after a grant or 100% progress milestone.</summary>
    private async Task SendNotificationAsync(BadgeNotification notification, CancellationToken ct = default)
    {
        var title = notification.IsNew
            ? $"New achievement: {notification.Title}"
            : $"Achievement earned: {notification.Title}";

        await _notifications.SendNotificationAsync(
            notification.UserId, "achievement", title,
            notification.Content,
            relatedEntityType: "Achievement",
            relatedEntityId: notification.AchievementId,
            priority: "medium", ct: ct);
    }
}
