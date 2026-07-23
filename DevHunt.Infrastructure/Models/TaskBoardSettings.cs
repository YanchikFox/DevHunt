using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Stores per-project task board display settings.
/// Each project has one settings record (1:1 relationship).
/// </summary>
public class TaskBoardSettings
{
    /// <summary>Unique settings identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Project identifier.</summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Current view mode: "board" (Trello-style) or "canvas" (Miro-style).
    /// </summary>
    [Required, MaxLength(10)]
    public string ViewMode { get; set; } = "board";

    // Canvas view settings

    /// <summary>
    /// Current zoom level (1.0 = 100%).
    /// </summary>
    public float CanvasZoom { get; set; } = 1.0f;

    /// <summary>
    /// Horizontal pan offset in pixels.
    /// </summary>
    public float CanvasPanX { get; set; } = 0;

    /// <summary>
    /// Vertical pan offset in pixels.
    /// </summary>
    public float CanvasPanY { get; set; } = 0;

    /// <summary>
    /// Whether to show completed tasks (done/archived/cancelled).
    /// </summary>
    public bool ShowCompletedTasks { get; set; } = true;

    /// <summary>
    /// Default column for new tasks (if null, uses first column).
    /// </summary>
    public Guid? DefaultColumnId { get; set; }

    /// <summary>Last update timestamp (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>Related project entity.</summary>
    public Project? Project { get; set; }
    /// <summary>Default column entity.</summary>
    public TaskColumn? DefaultColumn { get; set; }

    /// <summary>
    /// Valid view modes for validation.
    /// </summary>
    public static readonly HashSet<string> ValidViewModes = new() { "board", "canvas" };
}
