using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class NewsPostLikeConfiguration : IEntityTypeConfiguration<NewsPostLike>
{
    public void Configure(EntityTypeBuilder<NewsPostLike> builder)
    {
        builder.HasOne(l => l.NewsPost).WithMany(np => np.Likes).HasForeignKey(l => l.NewsPostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.NewsPostId, l.UserId }).IsUnique();
    }
}

public class NewsPostCommentConfiguration : IEntityTypeConfiguration<NewsPostComment>
{
    public void Configure(EntityTypeBuilder<NewsPostComment> builder)
    {
        builder.HasOne(c => c.NewsPost).WithMany(np => np.Comments).HasForeignKey(c => c.NewsPostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.NewsPostId, c.CreatedAt });
    }
}
