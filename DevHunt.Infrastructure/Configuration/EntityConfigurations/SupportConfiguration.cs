using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.HasOne(st => st.User).WithMany().HasForeignKey(st => st.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(st => st.AssignedToUser).WithMany().HasForeignKey(st => st.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(st => new { st.UserId, st.Status, st.CreatedAt });
    }
}

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.HasOne(tm => tm.Ticket).WithMany(st => st.Messages).HasForeignKey(tm => tm.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(tm => tm.Author).WithMany().HasForeignKey(tm => tm.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(tm => new { tm.TicketId, tm.CreatedAt });
    }
}

public class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.HasOne(th => th.Ticket).WithMany().HasForeignKey(th => th.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(th => th.ChangedByUser).WithMany().HasForeignKey(th => th.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(th => new { th.TicketId, th.CreatedAt });
        builder.HasIndex(th => th.ChangeType);
    }
}
