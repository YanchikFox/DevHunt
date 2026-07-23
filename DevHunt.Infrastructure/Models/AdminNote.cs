using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Internal admin note attached to a user account.
/// Only visible to admins/curators, not to the user.
/// </summary>
[Table("AdminNotes")]
public class AdminNote
{
    /// <summary>Primary key for an internal note on a DevHunt user account.</summary>
    public Guid Id { get; set; }

    /// <summary>User this note is about.</summary>
    public Guid UserId { get; set; }

    /// <summary>Admin who wrote the note.</summary>
    public Guid AuthorId { get; set; }

    /// <summary>Note content.</summary>
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the admin note was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    /// <summary>User account that this internal note describes.</summary>
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>Admin user who authored this internal note.</summary>
    [ForeignKey(nameof(AuthorId))]
    public User? Author { get; set; }
}
