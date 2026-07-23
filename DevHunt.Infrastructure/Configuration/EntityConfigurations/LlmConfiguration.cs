using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class UserApiKeyConfiguration : IEntityTypeConfiguration<UserApiKey>
{
    public void Configure(EntityTypeBuilder<UserApiKey> builder)
    {
        builder.HasOne(k => k.User)
            .WithMany()
            .HasForeignKey(k => k.UserId)
            // GDPR: deleting a user wipes their stored keys.
            .OnDelete(DeleteBehavior.Cascade);

        // One key per (user, provider) — enforced at DB level so a race on
        // concurrent POSTs can't dupe keys for the same provider.
        builder.HasIndex(k => new { k.UserId, k.Provider })
            .HasDatabaseName("IX_UserApiKeys_UserId_Provider")
            .IsUnique();
    }
}

public class LlmModelConfiguration : IEntityTypeConfiguration<LlmModel>
{
    public void Configure(EntityTypeBuilder<LlmModel> builder)
    {
        builder.Property(m => m.InputPricePer1M).HasPrecision(12, 4);
        builder.Property(m => m.OutputPricePer1M).HasPrecision(12, 4);

        builder.HasIndex(m => new { m.Provider, m.ModelId })
            .HasDatabaseName("IX_LlmModels_Provider_ModelId")
            .IsUnique();

        builder.HasIndex(m => new { m.IsEnabled, m.Provider, m.SortOrder })
            .HasDatabaseName("IX_LlmModels_Enabled_Provider_Sort");
    }
}
