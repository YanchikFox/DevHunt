using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class AiPlanConfiguration : IEntityTypeConfiguration<AiPlan>
{
    public void Configure(EntityTypeBuilder<AiPlan> builder)
    {
        builder.Property(p => p.PlanJson).HasColumnType("jsonb");
        builder.HasOne(p => p.Project).WithMany().HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.CreatedByUser).WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AppliedByUser).WithMany().HasForeignKey(p => p.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.ProjectId, p.CreatedAt }).HasDatabaseName("IX_AiPlans_Project_Created");
    }
}

public class AiOperationLogConfiguration : IEntityTypeConfiguration<AiOperationLog>
{
    public void Configure(EntityTypeBuilder<AiOperationLog> builder)
    {
        builder.Property(l => l.MetadataJson).HasColumnType("jsonb");
        builder.HasOne(l => l.Project).WithMany().HasForeignKey(l => l.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.Plan).WithMany(p => p.OperationLogs).HasForeignKey(l => l.PlanId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.ProjectId, l.CreatedAt }).HasDatabaseName("IX_AiOperationLogs_Project_Created");
        builder.HasIndex(l => l.PlanId).HasDatabaseName("IX_AiOperationLogs_Plan");
    }
}

public class ProjectArtifactConfiguration : IEntityTypeConfiguration<ProjectArtifact>
{
    public void Configure(EntityTypeBuilder<ProjectArtifact> builder)
    {
        builder.HasIndex(a => new { a.ProjectId, a.Type })
            .IsUnique()
            .HasDatabaseName("IX_ProjectArtifacts_Project_Type");

        builder.HasIndex(a => a.ProjectId)
            .HasDatabaseName("IX_ProjectArtifacts_Project");
    }
}
