using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Team membership entity linking a user to a project.
/// </summary>
/// <remarks>
/// Team member statuses:
/// - active: Currently part of the team
/// - inactive: Temporarily inactive
/// - left: Left the project (LeftAt is set)
///
/// Permissions are granular:
/// - CanPublishNews, CanManageTasks, CanManageFiles, CanManageGallery
/// - IsLeader grants elevated permissions (can invite, manage team)
///
/// Corresponds to ERD diagram: TeamMembers table.
/// </remarks>
public class TeamMember
{
    /// <summary>Unique membership identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>User ID of the team member.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Project this membership belongs to.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>Member's role in the project (developer, designer, devops, qa, etc.).</summary>
    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    /// <summary>Description of member's contributions to the project.</summary>
    [MaxLength(2000)]
    public string? Contribution { get; set; }

    /// <summary>Membership status: active, inactive, left.</summary>
    [MaxLength(24)]
    public string Status { get; set; } = "active";

    /// <summary>Whether member is a team leader (can manage team, send invitations).</summary>
    public bool IsLeader { get; set; } = false;

    /// <summary>Numeric score representing contribution level.</summary>
    public int? ContributionScore { get; set; }

    /// <summary>When member left the project (null = still active).</summary>
    public DateTime? LeftAt { get; set; }

    /// <summary>Permission: can publish news posts.</summary>
    public bool CanPublishNews { get; set; } = false;

    /// <summary>Permission: can create/edit/delete tasks.</summary>
    public bool CanManageTasks { get; set; } = false;

    /// <summary>Permission: can upload/delete project files.</summary>
    public bool CanManageFiles { get; set; } = false;

    /// <summary>Permission: can manage project gallery.</summary>
    public bool CanManageGallery { get; set; } = false;

    /// <summary>When member joined the team (UTC).</summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>The user who is a team member.</summary>
    public User? User { get; set; }

    /// <summary>The project this membership belongs to.</summary>
    public Project? Project { get; set; }
}
