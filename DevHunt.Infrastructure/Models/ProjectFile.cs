using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Project file (file exchange).
/// Allows the team to upload and share files through the platform.
/// </summary>
[Table("Project_Files")]
public class ProjectFile
{
    /// <summary>
    /// Unique identifier of the file.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Project identifier.
    /// </summary>
    [Required]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Identifier of the user who uploaded the file.
    /// </summary>
    [Required]
    public Guid UploadedById { get; set; }

    /// <summary>
    /// File name (original).
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File MIME type.
    /// </summary>
    [MaxLength(100)]
    public string? ContentType { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// File key in object storage (S3/MinIO).
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>
    /// File description (optional).
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// File category (e.g.: "documentation", "design", "code", "other").
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>
    /// Upload date and time.
    /// </summary>
    [Required]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update date and time.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Deletion date and time (soft delete).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Download count.
    /// </summary>
    public int DownloadCount { get; set; } = 0;

    /// <summary>
    /// File visibility scope (public, subscribers, members, private).
    /// </summary>
    [MaxLength(50)]
    public string Visibility { get; set; } = "public";

    // Navigation properties

    /// <summary>
    /// Project to which the file belongs.
    /// </summary>
    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    /// <summary>
    /// User who uploaded the file.
    /// </summary>
    [ForeignKey(nameof(UploadedById))]
    public User UploadedBy { get; set; } = null!;
}
