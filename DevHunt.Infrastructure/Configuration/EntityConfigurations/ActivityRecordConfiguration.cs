using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

/// <summary>
/// Entity configuration for ActivityRecord.
/// </summary>
public class ActivityRecordConfiguration : IEntityTypeConfiguration<ActivityRecord>
{
    public void Configure(EntityTypeBuilder<ActivityRecord> builder)
    {
        // PayloadJson is stored as jsonb in PostgreSQL
        builder.Property(a => a.PayloadJson).HasColumnType("jsonb");
    }
}
