using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevHunt.Infrastructure.Configuration.EntityConfigurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasOne(i => i.Project)
            .WithMany(p => p.Invitations)
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Inviter)
            .WithMany(u => u.SentInvitations)
            .HasForeignKey(i => i.InviterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Invitee)
            .WithMany(u => u.ReceivedInvitations)
            .HasForeignKey(i => i.InviteeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.ProjectId, i.InviteeId, i.Status });
        builder.HasIndex(i => new { i.InviteeId, i.Status });
    }
}
