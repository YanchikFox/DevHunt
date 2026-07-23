using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        // Composite primary key
        builder.HasKey(uf => new { uf.FollowerId, uf.FollowedId });
    }
}
