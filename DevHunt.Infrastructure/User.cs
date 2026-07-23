using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// User entity representing a registered account in DevHunt.
/// </summary>
/// <remarks>
/// User roles:
/// - participant: Regular user who can join projects
/// - company: Company account that can post internships
/// - curator: Mentor who can guide projects
/// - admin: System administrator
///
/// Authentication methods:
/// - Email/password (PasswordHash)
/// - GitHub OAuth (GithubId)
/// - Google OAuth (GoogleId)
///
/// Email verification is required before login (unless auto-verified in dev mode).
/// Corresponds to ERD diagram: Users table.
/// </remarks>
public class User
{
    /// <summary>Unique user identifier (UUID).</summary>
    public Guid Id { get; set; }

    /// <summary>User's email address (normalized to lowercase).</summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt-hashed password. Null for OAuth-only accounts.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>User's role: participant, company, curator, admin.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string? FullName { get; set; }

    /// <summary>Short unique nickname set by admin moderation (e.g. replacing inappropriate names).</summary>
    [MaxLength(50)]
    public string? Username { get; set; }

    /// <summary>User biography/description.</summary>
    public string? Bio { get; set; }

    /// <summary>User's timezone (e.g., "Europe/Moscow").</summary>
    public string? Timezone { get; set; }

    /// <summary>List of user's skills (legacy, use UserSkillEntries for new code).</summary>
    public List<string> Skills { get; set; } = new List<string>();

    /// <summary>Years of professional experience.</summary>
    public int? Experience { get; set; }

    /// <summary>Average rating from project reviews (1-5).</summary>
    public float? Rating { get; set; }

    /// <summary>URL to user's avatar image.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Whether user is verified by a curator.</summary>
    public bool IsVerified { get; set; } = false;

    /// <summary>Whether account is active (false = soft-deleted).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Account creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last profile update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Last successful login timestamp.</summary>
    public DateTime? LastLogin { get; set; }

    /// <summary>Preferred UI language (ru, en).</summary>
    public string? Language { get; set; }

    /// <summary>GitHub profile URL.</summary>
    public string? Github { get; set; }

    /// <summary>LinkedIn profile URL.</summary>
    public string? Linkedin { get; set; }

    /// <summary>Personal website URL.</summary>
    public string? Website { get; set; }

    // ========================================================================
    // OAuth identifiers
    // ========================================================================

    /// <summary>GitHub OAuth user ID (for account linking).</summary>
    public string? GithubId { get; set; }

    /// <summary>GitHub username (login) for API operations like issue assignment.</summary>
    public string? GithubUsername { get; set; }

    /// <summary>Google OAuth user ID (for account linking).</summary>
    public string? GoogleId { get; set; }

    // ========================================================================
    // Email verification
    // ========================================================================

    /// <summary>Whether email address has been verified.</summary>
    public bool IsEmailVerified { get; set; } = false;

    /// <summary>6-digit verification code sent via email.</summary>
    public string? VerificationToken { get; set; }

    /// <summary>Verification code expiration (24 hours from send).</summary>
    public DateTime? VerificationTokenExpiresAt { get; set; }

    // ========================================================================
    // Suspension
    // ========================================================================

    /// <summary>Account suspended until this date (null = not suspended).</summary>
    public DateTime? SuspendedUntil { get; set; }

    /// <summary>Reason for suspension (set by admin).</summary>
    public string? SuspensionReason { get; set; }

    // ========================================================================
    // Password reset
    // ========================================================================

    /// <summary>Secure token for password reset (URL-safe base64).</summary>
    public string? PasswordResetToken { get; set; }

    /// <summary>Password reset token expiration (1 hour from request).</summary>
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    // ========================================================================
    // Two-Factor Authentication (TOTP)
    // ========================================================================

    /// <summary>Whether TOTP 2FA is enabled for this account.</summary>
    public bool IsTotpEnabled { get; set; } = false;

    /// <summary>TOTP secret key (Base32-encoded, encrypted at rest). Null if 2FA not set up.</summary>
    [MaxLength(128)]
    public string? TotpSecret { get; set; }

    /// <summary>Comma-separated recovery codes (BCrypt-hashed). Used if authenticator unavailable.</summary>
    [MaxLength(2048)]
    public string? TotpRecoveryCodes { get; set; }

    // ========================================================================
    // Navigation properties
    // ========================================================================

    /// <summary>Projects the user is a member of.</summary>
    public ICollection<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();

    /// <summary>Tasks assigned to this user.</summary>
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();

    /// <summary>Invitations sent by this user.</summary>
    public ICollection<Invitation> SentInvitations { get; set; } = new List<Invitation>();

    /// <summary>Invitations received by this user.</summary>
    public ICollection<Invitation> ReceivedInvitations { get; set; } = new List<Invitation>();

    /// <summary>Moderation reports filed by this user.</summary>
    public ICollection<ModerationReport> Reports { get; set; } = new List<ModerationReport>();

    /// <summary>User's skills (legacy join table).</summary>
    public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();

    /// <summary>Achievements earned by this user.</summary>
    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();

    /// <summary>User's skill entries with proficiency levels.</summary>
    public ICollection<UserSkillEntry> UserSkillEntries { get; set; } = new List<UserSkillEntry>();

    /// <summary>Checks if user has admin or curator role.</summary>
    public bool IsAdminOrCurator() => Role is "admin" or "curator" or "superadmin";

    /// <summary>Checks if user has super admin role.</summary>
    public bool IsSuperAdmin() => Role == "superadmin";
}
