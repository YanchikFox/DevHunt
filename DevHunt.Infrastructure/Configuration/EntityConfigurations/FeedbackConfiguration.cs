using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class FeedbackItemConfiguration : IEntityTypeConfiguration<FeedbackItem>
{
    public void Configure(EntityTypeBuilder<FeedbackItem> builder)
    {
        builder.HasOne(fi => fi.Author).WithMany().HasForeignKey(fi => fi.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(fi => new { fi.Type, fi.Status, fi.CreatedAt });
        builder.HasIndex(fi => new { fi.VoteCount, fi.CreatedAt });
    }
}

public class FeedbackVoteConfiguration : IEntityTypeConfiguration<FeedbackVote>
{
    public void Configure(EntityTypeBuilder<FeedbackVote> builder)
    {
        builder.HasOne(fv => fv.Feedback).WithMany(fi => fi.Votes).HasForeignKey(fv => fv.FeedbackId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(fv => fv.User).WithMany().HasForeignKey(fv => fv.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(fv => new { fv.FeedbackId, fv.UserId }).IsUnique();
    }
}

public class FeedbackCommentConfiguration : IEntityTypeConfiguration<FeedbackComment>
{
    public void Configure(EntityTypeBuilder<FeedbackComment> builder)
    {
        builder.HasOne(fc => fc.Feedback).WithMany(fi => fi.Comments).HasForeignKey(fc => fc.FeedbackId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(fc => fc.Author).WithMany().HasForeignKey(fc => fc.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(fc => new { fc.FeedbackId, fc.CreatedAt });
    }
}
