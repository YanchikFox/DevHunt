using DevHunt.CoreApi.Models;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DevHunt.CoreApi.Security;

public interface IAuditService
{
    Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId = null, string? details = null, string? ipAddress = null, CancellationToken ct = default);
    Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId, string? details, string? ipAddress, string severity, CancellationToken ct = default);
    Task<List<AuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50, string? action = null, Guid? userId = null, string? severity = null, string? ip = null, string? entityType = null, DateTime? dateFrom = null, DateTime? dateTo = null, bool adminOnly = false, CancellationToken ct = default);
    Task<int> GetAuditLogCountAsync(string? action = null, Guid? userId = null, string? severity = null, string? ip = null, string? entityType = null, DateTime? dateFrom = null, DateTime? dateTo = null, bool adminOnly = false, CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly DevHuntDbContext _db;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<AuditService> _logger;

    public AuditService(DevHuntDbContext db, IHttpContextAccessor httpContext, ILogger<AuditService> logger)
    {
        _db = db;
        _httpContext = httpContext;
        _logger = logger;
    }

    public Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId = null, string? details = null, string? ipAddress = null, CancellationToken ct = default)
        => LogActionAsync(userId, action, entityType, entityId, details, ipAddress, "info", ct);

    public async Task LogActionAsync(Guid? userId, string action, string entityType, Guid? entityId, string? details, string? ipAddress, string severity, CancellationToken ct = default)
    {
        try
        {
            var userIdGuid = userId;
            if (!userIdGuid.HasValue)
            {
                var userIdClaim = _httpContext.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                userIdGuid = Guid.TryParse(userIdClaim, out var g) ? g : null;
            }

            // Prefer X-Real-IP (set by NGINX) so we log the actual client IP,
            // not the internal Docker/proxy network address.
            var rawIp = ipAddress
                ?? _httpContext.HttpContext?.Request?.Headers["X-Real-IP"].FirstOrDefault()
                ?? _httpContext.HttpContext?.Request?.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                ?? _httpContext.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            // Normalise IPv4-mapped IPv6 (::ffff:x.x.x.x → x.x.x.x)
            var ip = (rawIp != null && rawIp.StartsWith("::ffff:")) ? rawIp[7..] : rawIp;
            var userRole = _httpContext.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

            // Structured logging for real-time monitoring
            _logger.LogWarning("AUDIT: User={UserId} Role={UserRole} Action={Action} Entity={EntityType}/{EntityId} IP={Ip} Severity={Severity} Details={Details}",
                userIdGuid, userRole, action, entityType, entityId, ip, severity, details);

            // Persist to database for long-term audit trail
            var entry = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userIdGuid,
                UserRole = userRole,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                IpAddress = ip,
                Severity = severity,
                CreatedAt = DateTime.UtcNow
            };
            _db.AuditLogs.Add(entry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log");
        }
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50, string? action = null, Guid? userId = null, string? severity = null, string? ip = null, string? entityType = null, DateTime? dateFrom = null, DateTime? dateTo = null, bool adminOnly = false, CancellationToken ct = default)
    {
        var query = BuildAuditQuery(action, userId, severity, ip, entityType, dateFrom, dateTo, adminOnly);
        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetAuditLogCountAsync(string? action = null, Guid? userId = null, string? severity = null, string? ip = null, string? entityType = null, DateTime? dateFrom = null, DateTime? dateTo = null, bool adminOnly = false, CancellationToken ct = default)
        => await BuildAuditQuery(action, userId, severity, ip, entityType, dateFrom, dateTo, adminOnly).CountAsync(ct);

    private IQueryable<AuditLog> BuildAuditQuery(string? action, Guid? userId, string? severity, string? ip, string? entityType, DateTime? dateFrom, DateTime? dateTo, bool adminOnly)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (adminOnly)
            query = query.Where(a =>
                a.UserRole == UserRoles.Admin || a.UserRole == UserRoles.SuperAdmin || a.UserRole == UserRoles.Curator ||
                a.Action.StartsWith("admin.") || a.Action.StartsWith("superadmin."));

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action.Contains(action));
        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);
        if (!string.IsNullOrEmpty(severity))
            query = query.Where(a => a.Severity == severity);
        if (!string.IsNullOrEmpty(ip))
            query = query.Where(a => a.IpAddress != null && a.IpAddress.Contains(ip));
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (dateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= dateTo.Value);

        return query;
    }
}
