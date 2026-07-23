using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class TaskLinkConfiguration : IEntityTypeConfiguration<TaskLink>
{
    public void Configure(EntityTypeBuilder<TaskLink> builder)
    {
        builder.HasOne(tl => tl.SourceTask)
            .WithMany(t => t.OutgoingLinks)
            .HasForeignKey(tl => tl.SourceTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tl => tl.TargetTask)
            .WithMany(t => t.IncomingLinks)
            .HasForeignKey(tl => tl.TargetTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tl => tl.CreatedByUser)
            .WithMany()
            .HasForeignKey(tl => tl.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(tl => new { tl.SourceTaskId, tl.TargetTaskId, tl.LinkType }).IsUnique();
    }
}

public class TaskAttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
{
    public void Configure(EntityTypeBuilder<TaskAttachment> builder)
    {
        builder.HasOne(ta => ta.Task)
            .WithMany(t => t.Attachments)
            .HasForeignKey(ta => ta.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ta => ta.AttachedByUser)
            .WithMany()
            .HasForeignKey(ta => ta.AttachedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ta => ta.ProjectFile)
            .WithMany()
            .HasForeignKey(ta => ta.ProjectFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
