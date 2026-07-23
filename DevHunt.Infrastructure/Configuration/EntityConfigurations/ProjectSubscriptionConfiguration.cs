using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class ProjectSubscriptionConfiguration : IEntityTypeConfiguration<ProjectSubscription>
{
    public void Configure(EntityTypeBuilder<ProjectSubscription> builder)
    {
        // Composite primary key
        builder.HasKey(ps => new { ps.ProjectId, ps.UserId });
    }
}
