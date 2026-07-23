using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a typed relationship between two tasks.
/// Supports dependency tracking, blocking relationships, and more.
/// </summary>
public class TaskLink
{
    /// <summary>Unique link identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The task from which the link originates.
    /// </summary>
    [Required]
    public Guid SourceTaskId { get; set; }

    /// <summary>
    /// The task to which the link points.
    /// </summary>
    [Required]
    public Guid TargetTaskId { get; set; }

    /// <summary>
    /// Type of relationship:
    /// - blocks: Source task blocks the target
    /// - blocked_by: Source is blocked by target (inverse of blocks)
    /// - depends_on: Source depends on target completion
    /// - related_to: Informational link, no workflow impact
    /// - duplicate_of: Source is a duplicate of target
    /// - parent_of: Source is a parent task (epic/story)
    /// - child_of: Source is a subtask (inverse of parent_of)
    /// </summary>
    [Required, MaxLength(20)]
    public string LinkType { get; set; } = "related_to";

    /// <summary>User who created the link.</summary>
    public Guid CreatedByUserId { get; set; }
    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>Source task entity.</summary>
    public TaskItem? SourceTask { get; set; }
    /// <summary>Target task entity.</summary>
    public TaskItem? TargetTask { get; set; }
    /// <summary>Creator user entity.</summary>
    public User? CreatedByUser { get; set; }

    /// <summary>
    /// Valid link types for validation.
    /// </summary>
    public static readonly HashSet<string> ValidLinkTypes = new()
    {
        "blocks",
        "blocked_by",
        "depends_on",
        "related_to",
        "duplicate_of",
        "parent_of",
        "child_of"
    };

    /// <summary>
    /// Returns the inverse link type for bidirectional relationships.
    /// </summary>
    public static string? GetInverseLinkType(string linkType) => linkType switch
    {
        "blocks" => "blocked_by",
        "blocked_by" => "blocks",
        "parent_of" => "child_of",
        "child_of" => "parent_of",
        _ => null // related_to, depends_on, duplicate_of don't have automatic inverses
    };
}
