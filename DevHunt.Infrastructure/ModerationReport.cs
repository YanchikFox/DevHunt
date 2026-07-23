using System;
using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

public class ModerationReport
{
    public Guid Id { get; set; }

    [Required]
    public Guid ReporterId { get; set; }

    [Required]
    [MaxLength(32)]
    public string TargetType { get; set; } = string.Empty; // Project, Task, Message, User

    [Required]
    public Guid TargetId { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    public string Status { get; set; } = "pending";

    [MaxLength(200)]
    public string? ActionTaken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// ID пользователя-модератора, обработавшего репорт. Используется для триггеров достижений.
    /// </summary>
    public Guid? ProcessedByUserId { get; set; }

    public User? Reporter { get; set; }
}
