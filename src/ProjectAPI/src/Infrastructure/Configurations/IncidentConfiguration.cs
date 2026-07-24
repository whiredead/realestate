using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.FeedBacks.Entities;

namespace ProjectAPI.Infrastructure.Configurations;
/// <summary>
/// Configuration class for the <see cref="Incident"/> entity.
/// </summary>
public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    /// <summary>
    /// Configures the properties and relationships of the <see cref="Incident"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");

        // Configures the primary key for the Incident entity.
        builder.HasKey(i => i.Id);

        // Configures the UserId property to be required.
        builder.Property(i => i.UserId).IsRequired();

        // Configures the UnitId property to be required.
        builder.Property(i => i.UnitId).IsRequired();

        // Configures the Description property with a maximum length of 1000 characters and makes it required.
        builder.Property(i => i.Description).HasMaxLength(1000).IsRequired();

        // Configures the Status property with a maximum length of 50 characters and makes it required.
        builder.Property(i => i.Status).HasMaxLength(50).IsRequired();

        // Configures the ReportedAt property to be required.
        builder.Property(i => i.ReportedAt).IsRequired();
    }
}
