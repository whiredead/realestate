using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Common.Idempotency;

namespace ProjectAPI.Infrastructure.Configurations;

public class IdempotencyKeyRecordConfiguration : IEntityTypeConfiguration<IdempotencyKeyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyRecord> builder)
    {
        builder.ToTable("IdempotencyKeys");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Key).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Operation).IsRequired().HasMaxLength(200);
        builder.Property(r => r.RequestHash).IsRequired().HasMaxLength(64);
        builder.Property(r => r.ResponseTypeName).HasMaxLength(500);

        // §7 — the uniqueness guarantee itself: two concurrent inserts for the
        // same (Operation, Key) can't both succeed, so the loser gets a real
        // SQL conflict rather than a race the app code has to detect alone.
        builder.HasIndex(r => new { r.Operation, r.Key })
            .IsUnique()
            .HasDatabaseName("UX_IdempotencyKeys_OperationKey");
    }
}
