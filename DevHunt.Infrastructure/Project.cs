using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DevHunt.Infrastructure.Models;

/// <summary>Structured open role entry stored as JSONB.</summary>
public class OpenRoleEntry
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("totalNeeded")]
    public int TotalNeeded { get; set; } = 1;

    [JsonPropertyName("hoursPerWeek")]
    public int? HoursPerWeek { get; set; }

    [JsonPropertyName("equityOptional")]
    public bool EquityOptional { get; set; }
}

/// <summary>
/// Project model (Use Case UC-2: Project Creation, UC-5: Showcase)
/// 
/// Project lifecycle (State Machine):
/// - draft → recruiting → active → completed → archived
/// 
/// Visibility:
/// - public: Visible to everyone in the catalog
/// - private: Only for team members and owner
/// - unlisted: Accessible via direct link, but not in the public catalog
/// 
/// Corresponds to the ERD diagram from full_info_about_project/Last Claude/devhunt_erd.puml
/// Corresponds to SRS v1.0 section 4: Main platform features
/// </summary>
public class Project
{
    public Guid Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe handle for the project (e.g. "helix" in /projects/helix).
    /// Lowercase alphanumerics and hyphens, 2-48 chars. Unique across all projects when set.
    /// </summary>
    [MaxLength(48)]
    public string? Slug { get; set; }

    /// <summary>Total boosts received (denormalized count of <see cref="ProjectBoost"/> rows).</summary>
    public int BoostsCount { get; set; } = 0;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Stored as a PostgreSQL text[] column.
    /// </summary>
    public List<string> TechStack { get; set; } = new List<string>();

    [Required, MaxLength(50), ConcurrencyCheck]
    public string Status { get; set; } = "draft";

    [Required, MaxLength(50)]
    public string Visibility { get; set; } = "public";

    [Required]
    public Guid OwnerId { get; set; }
    [MaxLength(500)]
    public string? ShortDescription { get; set; }
    [MaxLength(24)]
    public string? DifficultyLevel { get; set; } // beginner/intermediate/advanced
    public int? ExpectedDurationDays { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool ShowcasePublished { get; set; } = false;
    public bool Featured { get; set; } = false;
    public float? Rating { get; set; }
    public int? MaxTeamSize { get; set; }
    public List<string> RequiredRoles { get; set; } = new List<string>();

    /// <summary>Structured open roles with hours/equity, stored as JSONB. Takes precedence over RequiredRoles.</summary>
    [Column(TypeName = "jsonb")]
    public List<OpenRoleEntry>? OpenRoles { get; set; }

    [MaxLength(50)]
    public string DefaultNewsVisibility { get; set; } = "public";
    [MaxLength(50)]
    public string DefaultFilesVisibility { get; set; } = "public";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
    public ICollection<ProjectTechStack> TechStacks { get; set; } = new List<ProjectTechStack>();
    public ICollection<ProjectRole> ProjectRoles { get; set; } = new List<ProjectRole>();
    public ICollection<ShowcaseProject> ShowcaseProjects { get; set; } = new List<ShowcaseProject>();
    public ICollection<ProjectFile> ProjectFiles { get; set; } = new List<ProjectFile>();
    public ICollection<ProjectDocument> ProjectDocuments { get; set; } = new List<ProjectDocument>();
    public ICollection<ProjectNewsPost> ProjectNewsPosts { get; set; } = new List<ProjectNewsPost>();
    public ICollection<ProjectSubscription> ProjectSubscriptions { get; set; } = new List<ProjectSubscription>();
    public ICollection<ActivityRecord> ActivityRecords { get; set; } = new List<ActivityRecord>();

    public ICollection<ProjectBoost> Boosts { get; set; } = new List<ProjectBoost>();
}

/// <summary>
/// Represents a "boost" (GitHub-star-style endorsement) by a user on a project.
/// One row per (ProjectId, UserId) — users can boost each project at most once.
/// </summary>
public class ProjectBoost
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
