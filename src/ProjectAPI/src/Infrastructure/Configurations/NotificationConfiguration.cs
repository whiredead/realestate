using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Notifications.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired().HasMaxLength(450);
        builder.Property(n => n.Type).IsRequired().HasMaxLength(50);
        builder.Property(n => n.TitleFr).IsRequired().HasMaxLength(200);
        builder.Property(n => n.TitleEn).HasMaxLength(200);
        builder.Property(n => n.BodyFr).IsRequired().HasMaxLength(2000);
        builder.Property(n => n.BodyEn).HasMaxLength(2000);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(50);

        builder.HasIndex(n => new { n.UserId, n.IsRead });
    }
}
