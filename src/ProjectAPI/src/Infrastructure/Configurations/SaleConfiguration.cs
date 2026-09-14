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

        b.Property(x => x.Status)
            .HasConversion<int>()   // the filtered indexes below match on 0/1/2
            .IsRequired();

        b.Property(x => x.FinalPrice).HasPrecision(18, 2);
        b.Property(x => x.ReservationAmount).HasPrecision(18, 2);
        b.Property(x => x.RemainingAmount).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).HasMaxLength(450);

        // §6 — at most ONE active sale per reservation and per unit. Filtered
        // unique indexes are the same device already used for
        // IX_Reservations_ActivePerUnit: the application check in
        // CreateSaleDraftHandler is a read-then-write and cannot survive two
        // concurrent requests on its own.
        //
        // The filter is written in the PERSISTED representation: Status is
        // stored as int (HasConversion<int> above), so the active set
        // Draft/PendingNotary/Confirmed is (0, 1, 2). Cancelled (3) is excluded
        // so a cancelled sale frees both the reservation and the unit.
        // MUST stay in sync with SaleStateMachine.ActiveStatuses.
        //
        // ApiExceptionFilter matches these index names to return 409 rather
        // than a bare 500, so the names are part of the contract.
        b.HasIndex(x => x.ReservationId, "IX_Sales_ActivePerReservation")
            .IsUnique()
            .HasFilter("[ReservationId] IS NOT NULL AND [Status] IN (0, 1, 2)");

        b.HasIndex(x => x.UnitId, "IX_Sales_ActivePerUnit")
            .IsUnique()
            .HasFilter("[Status] IN (0, 1, 2)");

        // Plain FK index: the unique one above only covers active rows, so
        // lookups over a reservation's cancelled sales still need this.
        b.HasIndex(x => x.ReservationId, "IX_Sales_ReservationId");

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
