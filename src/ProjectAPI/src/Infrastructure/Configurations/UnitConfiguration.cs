using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="Unit"/> entity.
    /// </summary>
    public class UnitConfiguration : IEntityTypeConfiguration<Unit>
    {
        /// <summary>
        /// Configures the properties and relationships of the <see cref="Unit"/> entity.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity.</param>
        public void Configure(EntityTypeBuilder<Unit> builder)
        {
            builder.ToTable("Units");

            // Defines the primary key for Unit.
            builder.HasKey(unit => unit.Id);

            // Configures the Floor property.
            builder.Property(unit => unit.Floor)
                .HasMaxLength(50);

            // Configures the UnitNumber property.
            builder.Property(unit => unit.UnitNumber)
                .HasMaxLength(50);

            // Configures the NumberOfBedrooms property.
            builder.Property(unit => unit.NumberOfBedrooms);

            // Configures the NumberOfBathrooms property.
            builder.Property(unit => unit.NumberOfBathrooms);

            // Configures the ApartmentSurface property.
            builder.Property(unit => unit.ApartmentSurface);

            // Configures the BalconySurface property.
            builder.Property(unit => unit.BalconySurface);

            // Configures the TerraceSurface property.
            builder.Property(unit => unit.TerraceSurface);

            // Configures the GardenSurface property.
            builder.Property(unit => unit.GardenSurface);

            // Configures the View property.
            builder.Property(unit => unit.View)
                .HasMaxLength(150);

            // Configures the Orientation property.
            builder.Property(unit => unit.Orientation)
                .HasMaxLength(100);

            // Configures the TotalSurface property.
            builder.Property(unit => unit.TotalSurface);

            // Configures the SaleableValue property.
            builder.Property(unit => unit.SaleableValue);

            // Configures the SaleableValue1 property.
            builder.Property(unit => unit.SaleableValue1);

            // Configures the PriceSaleableValue property.
            builder.Property(unit => unit.PriceSaleableValue);

            // Configures the PriceSaleableValue1 property.
            builder.Property(unit => unit.PriceSaleableValue1);

            // Configures the LatestPrice property.
            builder.Property(unit => unit.LatestPrice);

            // Configures the relationship between Unit and Project.
            builder.HasOne(unit => unit.Immeuble)
                .WithMany(project => project.Units)
                .HasForeignKey(unit => unit.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configures the relationship between Unit and PropertyDeliveries.
            builder.HasMany(unit => unit.PropertyDeliveries)
                .WithOne(delivery => delivery.Unit)
                .HasForeignKey(delivery => delivery.UnitId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
