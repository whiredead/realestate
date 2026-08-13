using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Invitations.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class InternalInvitationProjectAssignmentConfiguration : IEntityTypeConfiguration<InternalInvitationProjectAssignment>
{
    public void Configure(EntityTypeBuilder<InternalInvitationProjectAssignment> builder)
    {
        builder.ToTable("InternalInvitationProjectAssignments");

        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.InternalInvitation)
               .WithMany(i => i.ProjectAssignments)
               .HasForeignKey(a => a.InternalInvitationId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: deleting a project must never silently
        // delete a pending invitation — mirrors ProjectMembershipConfiguration's
        // reasoning for its own FKs.
        builder.HasOne(a => a.Project)
               .WithMany()
               .HasForeignKey(a => a.ProjectId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.InternalInvitationId, a.ProjectId })
               .HasDatabaseName("IX_InternalInvitationProjectAssignments_InvitationId_ProjectId")
               .IsUnique();
    }
}
