using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Invitations.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class InternalInvitationConfiguration : IEntityTypeConfiguration<InternalInvitation>
{
    public void Configure(EntityTypeBuilder<InternalInvitation> builder)
    {
        builder.ToTable("InternalInvitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.RoleCode).HasMaxLength(30).IsRequired();
        builder.Property(i => i.InvitedByUserId).HasMaxLength(450).IsRequired();

        // 64 hex chars = SHA-256 digest. Never store the raw token — see
        // CreateInternalInvitationHandler's doc comment.
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();

        builder.Property(i => i.RevokedByUserId).HasMaxLength(450).IsRequired(false);
        builder.Property(i => i.AcceptedUserId).HasMaxLength(450).IsRequired(false);

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.ExpiresAt).IsRequired();

        // A hash collision must never resolve to the wrong invitation.
        builder.HasIndex(i => i.TokenHash)
               .HasDatabaseName("IX_InternalInvitations_TokenHash")
               .IsUnique();

        builder.HasIndex(i => i.Email)
               .HasDatabaseName("IX_InternalInvitations_Email");
    }
}
