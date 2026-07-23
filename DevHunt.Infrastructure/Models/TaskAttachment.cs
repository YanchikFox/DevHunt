using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Represents a file attachment on a task.
/// Can either reference an existing ProjectFile or store a new upload.
/// </summary>
public class TaskAttachment
{
    /// <summary>Unique attachment identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Task identifier.</summary>
    [Required]
    public Guid TaskId { get; set; }

    /// <summary>
    /// Reference to an existing project file.
    /// If set, this attachment links to that file without duplication.
    /// </summary>
    public Guid? ProjectFileId { get; set; }

    // Fields for inline uploads (when ProjectFileId is null)

    /// <summary>
    /// Original filename of the uploaded file.
    /// </summary>
    [MaxLength(255)]
    public string? FileName { get; set; }

    /// <summary>
    /// MIME content type (e.g., "image/png", "application/pdf").
    /// </summary>
    [MaxLength(100)]
    public string? ContentType { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// Storage key for retrieving the file from object storage.
    /// Only set for inline uploads, not for ProjectFile references.
    /// </summary>
    [MaxLength(500)]
    public string? StorageKey { get; set; }

    /// <summary>
    /// User who attached the file.
    /// </summary>
    public Guid AttachedByUserId { get; set; }

    /// <summary>Attachment timestamp (UTC).</summary>
    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>Related task entity.</summary>
    public TaskItem? Task { get; set; }
    /// <summary>Related project file entity.</summary>
    public ProjectFile? ProjectFile { get; set; }
    /// <summary>User who attached the file.</summary>
    public User? AttachedByUser { get; set; }

    /// <summary>
    /// Gets the effective filename (from ProjectFile or direct upload).
    /// </summary>
    public string GetEffectiveFileName() =>
        ProjectFile?.FileName ?? FileName ?? "Unknown";

    /// <summary>
    /// Gets the effective content type.
    /// </summary>
    public string? GetEffectiveContentType() =>
        ProjectFile?.ContentType ?? ContentType;

    /// <summary>
    /// Gets the effective file size.
    /// </summary>
    public long? GetEffectiveFileSize() =>
        ProjectFile?.FileSize ?? FileSize;
}
