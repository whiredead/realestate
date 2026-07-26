using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Crm.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class CrmContactConfiguration : IEntityTypeConfiguration<CrmContact>
{
    public void Configure(EntityTypeBuilder<CrmContact> builder)
    {
        builder.ToTable("CrmContacts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ContactNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(c => c.ContactNumber).IsUnique();

        builder.Property(c => c.FirstName).HasMaxLength(120).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Cin).HasMaxLength(40);
        builder.Property(c => c.Email).HasMaxLength(320);
        builder.Property(c => c.EmailNormalized).HasMaxLength(320);
        builder.Property(c => c.Phone).HasMaxLength(40);
        builder.Property(c => c.PhoneNormalized).HasMaxLength(40);
        builder.Property(c => c.PreferredLanguage).HasMaxLength(5).IsRequired();
        builder.Property(c => c.AcquisitionSourceCode).HasMaxLength(50);
        builder.Property(c => c.OwnerSalesAgentId).HasMaxLength(450);
        builder.Property(c => c.UserId).HasMaxLength(450);

        // §6.1 — statuses are varchar + CHECK, never a database enum, so the
        // column survives a migration that renames or reorders the CLR enum.
        builder.Property(c => c.LifecycleStatus)
            .HasMaxLength(30)
            .IsRequired()
            .HasConversion(
                status => status.ToCode(),
                value => ContactLifecycleCodes.Parse(value));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CrmContacts_LifecycleStatus",
            $"[LifecycleStatus] IN ('{string.Join("','", ContactLifecycleCodes.All)}')"));

        // Matching indexes. Neither is unique: §1.1 makes a phone deliberately
        // non-unique (households share one) and an email unique only among
        // accounts, not people. They exist to make duplicate DETECTION fast —
        // the decision to merge stays human (§9).
        builder.HasIndex(c => c.EmailNormalized).HasDatabaseName("IX_CrmContacts_EmailNormalized");
        builder.HasIndex(c => c.PhoneNormalized).HasDatabaseName("IX_CrmContacts_PhoneNormalized");

        // One account maps to at most one contact — the guarantee §1.1 states as
        // `users.crm_contact_id` unique-when-non-null, expressed from this side.
        // Filtered so the many account-less contacts do not collide on NULL.
        builder.HasIndex(c => c.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL")
            .HasDatabaseName("UX_CrmContacts_UserId");
    }
}
