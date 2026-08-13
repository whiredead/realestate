using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Crm.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AccountInvitationConfiguration : IEntityTypeConfiguration<AccountInvitation>
{
    public void Configure(EntityTypeBuilder<AccountInvitation> builder)
    {
        builder.ToTable("AccountInvitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Token).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Email).IsRequired().HasMaxLength(150);

        builder.HasIndex(i => i.Token).IsUnique();
        builder.HasIndex(i => i.CrmContactId);

        builder.HasOne(i => i.CrmContact)
            .WithMany()
            .HasForeignKey(i => i.CrmContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
