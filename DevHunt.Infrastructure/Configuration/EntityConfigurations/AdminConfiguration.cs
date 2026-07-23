using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class ProjectIssueConfiguration : IEntityTypeConfiguration<ProjectIssue>
{
    public void Configure(EntityTypeBuilder<ProjectIssue> builder)
    {
        builder.HasOne(pi => pi.Project).WithMany().HasForeignKey(pi => pi.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(pi => pi.Reporter).WithMany().HasForeignKey(pi => pi.ReporterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(pi => pi.AssignedToAdmin).WithMany().HasForeignKey(pi => pi.AssignedToAdminId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(pi => new { pi.ProjectId, pi.Status, pi.CreatedAt });
        builder.HasIndex(pi => new { pi.Status, pi.Priority, pi.CreatedAt });
    }
}

public class UserPrivacySettingsConfiguration : IEntityTypeConfiguration<UserPrivacySettings>
{
    public void Configure(EntityTypeBuilder<UserPrivacySettings> builder)
    {
        builder.HasOne(ups => ups.User).WithMany().HasForeignKey(ups => ups.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(ups => ups.UserId).IsUnique();
    }
}
