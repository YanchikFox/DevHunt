using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Task entity representing a work item in a project's Kanban board.
/// </summary>
/// <remarks>
/// Task status workflow:
/// - todo → doing → review → done
/// - archived/cancelled are terminal states
///
/// Supports:
/// - Assignment to team members
/// - Time tracking (estimated vs actual hours)
/// - Custom Kanban columns
/// - Task linking (dependencies)
/// - File attachments
/// - Soft delete with restore capability
///
/// Corresponds to ERD diagram: TaskItems table.
/// </remarks>
public class TaskItem
{
    /// <summary>Unique task identifier (UUID).</summary>
    public Guid Id { get; set; }

    /// <summary>Parent project ID.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>Task title (max 200 chars).</summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Current status: todo, doing, review, done, archived, cancelled.</summary>
    [Required, MaxLength(50)]
    public string Status { get; set; } = "todo";

    /// <summary>Assigned team member ID (null = unassigned).</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>Task deadline (null = no deadline).</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Task description (supports markdown, max 1000 chars).</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Priority level: low, medium (default), high, urgent.</summary>
    [MaxLength(32)]
    public string? Priority { get; set; } = "medium";

    /// <summary>User who created this task.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Task creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>When task was marked as done (auto-set on status change).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Estimated hours to complete.</summary>
    public float? EstimatedHours { get; set; }

    /// <summary>Actual hours spent on task.</summary>
    public float? ActualHours { get; set; }

    /// <summary>Soft delete flag (true = hidden from UI but not deleted).</summary>
    public bool IsDeleted { get; set; } = false;

    // Custom column support
    /// <summary>
    /// Reference to the column this task belongs to.
    /// If null, task will be placed in the first (default) column.
    /// </summary>
    public Guid? ColumnId { get; set; }

    /// <summary>
    /// Position within the column (0-based, lower = higher in list).
    /// </summary>
    public int PositionInColumn { get; set; } = 0;

    // Canvas mode positioning (for individual task cards)
    public float? CanvasX { get; set; }
    public float? CanvasY { get; set; }

    // Tags support
    /// <summary>
    /// Comma-separated tags for filtering (e.g., "Frontend,Design").
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    // GitHub Issue sync fields
    /// <summary>
    /// GitHub Issue unique ID (from GitHub API). Used for bidirectional sync.
    /// </summary>
    public long? GitHubIssueId { get; set; }

    /// <summary>
    /// GitHub Issue number (e.g., #42). Human-readable identifier.
    /// </summary>
    public int? GitHubIssueNumber { get; set; }

    /// <summary>
    /// Direct URL to the GitHub Issue (e.g., https://github.com/owner/repo/issues/42).
    /// </summary>
    [MaxLength(500)]
    public string? GitHubIssueUrl { get; set; }

    // Navigation properties
    public Project? Project { get; set; }
    public User? AssignedToUser { get; set; }
    public TaskColumn? Column { get; set; }

    /// <summary>
    /// Links where this task is the source.
    /// </summary>
    public ICollection<TaskLink> OutgoingLinks { get; set; } = new List<TaskLink>();

    /// <summary>
    /// Links where this task is the target.
    /// </summary>
    public ICollection<TaskLink> IncomingLinks { get; set; } = new List<TaskLink>();

    /// <summary>
    /// Files attached to this task.
    /// </summary>
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
}
