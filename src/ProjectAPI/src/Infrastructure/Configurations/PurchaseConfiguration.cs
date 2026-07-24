using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Purchases.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="Purchase"/> entity.
    /// </summary>
    public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
    {
        /// <summary>
        /// Configures the properties and relationships of the <see cref="Purchase"/> entity.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity.</param>
        public void Configure(EntityTypeBuilder<Purchase> builder)
        {
            // Set the table name
            builder.ToTable("Purchases");

            // Primary key configuration
            builder.HasKey(p => p.Id);

            // Configure the UserId property with the appropriate SQL column type.
            builder.Property(p => p.UserId)
                   .IsRequired()
                   .HasColumnType("nvarchar(450)");

            // Configure optional foreign key properties.
            builder.Property(p => p.SaleId)
                   .IsRequired(false);
            builder.Property(p => p.ReservationId)
                   .IsRequired(false);
            builder.Property(p => p.NotaryAppointmentId)
                   .IsRequired(false);

            // Configure the financial properties with proper precision.
            builder.Property(p => p.TotalPrice)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");
            builder.Property(p => p.PaidAmount)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");
            builder.Property(p => p.RemainingAmount)
                   .IsRequired()
                   .HasColumnType("decimal(18,2)");

            // Configure the CreatedAt property.
            builder.Property(p => p.CreatedAt)
                   .IsRequired();

            // Configure the one-to-many relationship with PurchaseDocument.
          /*  builder.HasMany(p => p.Documents)
                   .WithOne(d => d.Purchase)
                   .HasForeignKey(d => d.PurchaseId)
                   .OnDelete(DeleteBehavior.Cascade);*/
        }
    }
}
