using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a custom column in a project's task board.
/// Columns can be reordered and positioned freely in canvas mode.
/// </summary>
public class TaskColumn
{
    /// <summary>Unique column identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Project identifier.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>Column name.</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Order of the column in board view (0-based).
    /// </summary>
    public int Position { get; set; }

    // Canvas mode positioning
    /// <summary>Canvas X coordinate.</summary>
    public float? CanvasX { get; set; }
    /// <summary>Canvas Y coordinate.</summary>
    public float? CanvasY { get; set; }
    /// <summary>Canvas width.</summary>
    public float? CanvasWidth { get; set; }
    /// <summary>Canvas height.</summary>
    public float? CanvasHeight { get; set; }

    /// <summary>
    /// Hex color code for the column header (e.g., "#3B82F6").
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }

    /// <summary>
    /// Default columns cannot be deleted (created during project initialization).
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// When true, tasks moved to this column are considered completed.
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// Optional limit on number of tasks in this column (WIP limit).
    /// Null means no limit.
    /// </summary>
    public int? WipLimit { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    /// <summary>Related project entity.</summary>
    public Project? Project { get; set; }
    /// <summary>Tasks in this column.</summary>
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
