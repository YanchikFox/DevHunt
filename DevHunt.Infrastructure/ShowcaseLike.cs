using DevHunt.Infrastructure.Models;

namespace DevHunt.Infrastructure;

/// <summary>
/// Per-user like on a project showcase. The composite (ShowcaseId, UserId) primary key makes a
/// like idempotent per user, so the denormalized <see cref="ShowcaseProject.LikesCount"/> can no
/// longer be spammed or driven below the real count via repeated like/unlike calls (DEV-114).
/// </summary>
public class ShowcaseLike
{
    /// <summary>Showcase that was liked (references <see cref="ShowcaseProject.Id"/>).</summary>
    public Guid ShowcaseId { get; set; }

    /// <summary>The liked showcase.</summary>
    public ShowcaseProject Showcase { get; set; } = default!;

    /// <summary>User who liked the showcase.</summary>
    public Guid UserId { get; set; }

    /// <summary>The user who liked the showcase.</summary>
    public User User { get; set; } = default!;

    /// <summary>When the like was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
