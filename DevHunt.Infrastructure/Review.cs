using System;
using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

public class Review
{
    public Guid Id { get; set; }

    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid ReviewerId { get; set; }

    public Guid? ReviewedUserId { get; set; }

    [Range(1,5)]
    public int Rating { get; set; }

    [MaxLength(2000)]
    public string? ReviewText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
    public User? Reviewer { get; set; }
    public User? ReviewedUser { get; set; }
}
