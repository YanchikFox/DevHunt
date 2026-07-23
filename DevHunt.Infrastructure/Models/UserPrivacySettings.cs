using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// User profile privacy settings
/// Allows managing visibility of various parts of the profile
/// </summary>
public class UserPrivacySettings
{
    /// <summary>Unique settings identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>User identifier.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Profile visibility: public, private, friends_only
    /// </summary>
    [MaxLength(50)]
    public string ProfileVisibility { get; set; } = "public"; // public, private, friends_only

    /// <summary>
    /// Show email in public profile
    /// </summary>
    public bool ShowEmail { get; set; } = false;

    /// <summary>
    /// Show skills in public profile
    /// </summary>
    public bool ShowSkills { get; set; } = true;

    /// <summary>
    /// Show experience in public profile
    /// </summary>
    public bool ShowExperience { get; set; } = true;

    /// <summary>
    /// Show rating in public profile
    /// </summary>
    public bool ShowRating { get; set; } = true;

    /// <summary>
    /// Show projects in public profile
    /// </summary>
    public bool ShowProjects { get; set; } = true;

    /// <summary>
    /// Show social links (GitHub, LinkedIn, Website)
    /// </summary>
    public bool ShowSocialLinks { get; set; } = true;

    /// <summary>
    /// Show achievements
    /// </summary>
    public bool ShowAchievements { get; set; } = true;

    /// <summary>
    /// Allow email search
    /// </summary>
    public bool AllowEmailSearch { get; set; } = false;

    /// <summary>
    /// Receive notifications about new messages
    /// </summary>
    public bool NotifyOnMessages { get; set; } = true;

    /// <summary>
    /// Receive notifications about project invitations
    /// </summary>
    public bool NotifyOnInvitations { get; set; } = true;

    /// <summary>Activity visibility scope (public, followers, private).</summary>
    [MaxLength(50)]
    public string ActivityVisibility { get; set; } = "public";

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    /// <summary>Related user entity.</summary>
    public User? User { get; set; }
}

