using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class ReservationBuyerConfiguration : IEntityTypeConfiguration<ReservationBuyer>
{
    public void Configure(EntityTypeBuilder<ReservationBuyer> builder)
    {
        builder.ToTable("ReservationBuyers");
        builder.HasKey(rb => rb.Id);

        builder.Property(rb => rb.OwnershipPercent).HasColumnType("decimal(7,4)");

        builder.HasOne(rb => rb.Reservation)
            .WithMany(r => r.Buyers)
            .HasForeignKey(rb => rb.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rb => rb.CrmContact)
            .WithMany()
            .HasForeignKey(rb => rb.CrmContactId)
            .OnDelete(DeleteBehavior.Restrict);

        // §6.2 — the same person is not added twice as a co-buyer on one file.
        builder.HasIndex(rb => new { rb.ReservationId, rb.CrmContactId })
            .IsUnique()
            .HasDatabaseName("UX_ReservationBuyers_ReservationContact");
    }
}
