using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Sales.Entities;
namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="PropertyDelivery"/> entity.
    /// </summary>
    public class PropertyDeliveryConfiguration : IEntityTypeConfiguration<PropertyDelivery>
    {
        /// <summary>
        /// Configures the properties and relationships of the <see cref="PropertyDelivery"/> entity.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity.</param>
        public void Configure(EntityTypeBuilder<PropertyDelivery> builder)
        {
            // Table name
            builder.ToTable("PropertyDeliveries");

            // Primary Key
            builder.HasKey(pd => pd.Id);

            // SaleId property configuration
            builder.Property(pd => pd.SaleId)
                .IsRequired();

            // UnitId property configuration
            builder.Property(pd => pd.UnitId)
                .IsRequired();

            // DeliveryDate property configuration
            builder.Property(pd => pd.DeliveryDate)
                .IsRequired();

            // Status property configuration
            builder.Property(pd => pd.Status)
                .IsRequired()
                .HasMaxLength(50);

            // Report property configuration
            builder.Property(pd => pd.Report)
                .HasMaxLength(1000);

            // Relationships
            builder.HasOne(pd => pd.Sale)
                .WithMany(s => s.PropertyDeliveries)
                .HasForeignKey(pd => pd.SaleId);

            builder.HasOne(pd => pd.Unit)
                 .WithMany(u => u.PropertyDeliveries)
                 .HasForeignKey(pd => pd.UnitId);
        }
    }
}
