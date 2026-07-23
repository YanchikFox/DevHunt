using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Persistent audit log entry for tracking privileged actions.
/// Stores all admin/superadmin operations for security compliance.
/// </summary>
[Table("AuditLogs")]
public class AuditLog
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>ID of the user who performed the action.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Role of the user at the time of action (for historical accuracy).</summary>
    [MaxLength(50)]
    public string? UserRole { get; set; }

    /// <summary>Action identifier, e.g. "user.hard_deleted", "role.changed".</summary>
    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Type of entity affected (User, Project, etc.).</summary>
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>ID of the affected entity.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Human-readable details of the action.</summary>
    public string? Details { get; set; }

    /// <summary>IP address of the requester.</summary>
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>Severity level: info, warning, critical.</summary>
    [MaxLength(20)]
    public string Severity { get; set; } = "info";

    /// <summary>Timestamp of the action (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
