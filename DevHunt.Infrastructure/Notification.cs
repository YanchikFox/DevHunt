using System;
using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

public class Notification
{
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string Type { get; set; } = "general";

    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Content { get; set; }

    [MaxLength(50)]
    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    [MaxLength(16)]
    public string? Priority { get; set; } = "low";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public bool IsRead { get; set; } = false;
}
