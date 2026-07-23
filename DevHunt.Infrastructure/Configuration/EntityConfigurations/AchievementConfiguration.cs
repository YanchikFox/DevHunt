using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

/// <summary>
/// A unique composite index (UserId, AchievementId) guarantees at the DB level
/// that a user cannot receive the same badge twice.
/// This eliminates the TOCTOU race in BadgesService.AwardBadgeAsync: instead of an
/// AnyAsync check, we rely on a DbUpdateException on uniqueness violation.
/// </summary>
public class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.HasIndex(ua => new { ua.UserId, ua.AchievementId })
               .IsUnique()
               .HasDatabaseName("IX_User_Achievements_UserId_AchievementId_Unique");
    }
}

/// <summary>
/// A unique index on Code guarantees that two badges with the same code
/// cannot be created by concurrent requests (TOCTOU in CreateBadgeAsync).
/// </summary>
public class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.HasIndex(a => a.Code)
               .IsUnique()
               .HasDatabaseName("IX_Achievements_Code_Unique");
    }
}
