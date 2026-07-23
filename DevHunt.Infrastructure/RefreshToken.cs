using System.ComponentModel.DataAnnotations;

namespace DevHunt.Infrastructure.Models;

/// <summary>
/// Refresh token for JWT authentication.
/// Used to obtain new access tokens without re-authentication.
/// 
/// SECURITY (R4): Tokens are stored as HMAC-SHA256 hash, not in plain text.
/// This protects against token theft in case of database leak.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Rotation family id — all tokens in a chain share this value for reuse detection.
    /// </summary>
    [Required]
    public Guid TokenFamilyId { get; set; }
    
    /// <summary>
    /// HMAC-SHA256 hash of the token (not the token itself!).
    /// Client receives the original token, but we only store the hash in the database.
    /// </summary>
    [Required, MaxLength(64)] // SHA256 hex = 64 chars
    public string TokenHash { get; set; } = string.Empty;
    
    [Required]
    public DateTime ExpiresAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last token usage time for reuse detection
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
    
    /// <summary>
    /// Usage count (for anomaly monitoring)
    /// </summary>
    public int UsageCount { get; set; } = 0;
    
    public bool IsRevoked { get; set; } = false;
    
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>
    /// Revocation reason (e.g., "reuse_detected", "user_logout", "expired")
    /// </summary>
    [MaxLength(100)]
    public string? RevocationReason { get; set; }
    
    // Navigation property
    public User? User { get; set; }
}

