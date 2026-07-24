namespace ProjectAPI.Infrastructure.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Sales.Entities;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("Sales");
        b.HasKey(x => x.Id);

        b.Property(x => x.BuyerId).HasMaxLength(450);

        b.Property(x => x.BuyerFirstName).IsRequired().HasMaxLength(150);
        b.Property(x => x.BuyerLastName).IsRequired().HasMaxLength(150);
        b.Property(x => x.BuyerEmail).IsRequired().HasMaxLength(256);
        b.Property(x => x.BuyerPhoneNumber).IsRequired().HasMaxLength(50);
        b.Property(x => x.BuyerCIN).HasMaxLength(50); // Nullable - CIN not always available for registered users

        b.Property(x => x.TotalPrice).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.SaleDate).IsRequired();

        b.HasMany(x => x.Payments)
            .WithOne()
            .HasForeignKey(p => p.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.PropertyDeliveries)
            .WithOne(d => d.Sale)
            .HasForeignKey(d => d.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
