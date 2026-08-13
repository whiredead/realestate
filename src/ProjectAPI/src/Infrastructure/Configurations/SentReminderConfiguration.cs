using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Notifications.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class SentReminderConfiguration : IEntityTypeConfiguration<SentReminder>
{
    public void Configure(EntityTypeBuilder<SentReminder> builder)
    {
        builder.ToTable("SentReminders");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.EntityType).IsRequired().HasMaxLength(50);

        // One reminder per (EntityType, EntityId): the job's own idempotence guarantee.
        builder.HasIndex(s => new { s.EntityType, s.EntityId })
            .IsUnique()
            .HasDatabaseName("UX_SentReminders_EntityTypeId");
    }
}
