using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Follow relationship between two users.
/// </summary>
[Table("User_Follows")]
public class UserFollow
{
    /// <summary>Follower user ID.</summary>
    public Guid FollowerId { get; set; }
    /// <summary>Followed user ID.</summary>
    public Guid FollowedId { get; set; }
    /// <summary>Follow creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Follower user entity.</summary>
    [ForeignKey(nameof(FollowerId))]
    public User Follower { get; set; } = null!;

    /// <summary>Followed user entity.</summary>
    [ForeignKey(nameof(FollowedId))]
    public User Followed { get; set; } = null!;
}
