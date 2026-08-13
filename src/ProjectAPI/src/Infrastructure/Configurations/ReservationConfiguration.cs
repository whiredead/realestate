using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.ToTable("Reservations");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name).HasMaxLength(100).IsRequired(false);
            builder.Property(r => r.LastName).HasMaxLength(100).IsRequired(false);
            builder.Property(r => r.CIN).HasMaxLength(50).IsRequired(false);
            builder.Property(r => r.Email).HasMaxLength(150).IsRequired(false);
            builder.Property(r => r.PhoneNumber).HasMaxLength(20).IsRequired(false);

            builder.Property(r => r.TotalPropertyPrice).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(r => r.ReservationAmount).HasColumnType("decimal(18,2)").IsRequired();

            // §5.3 — snapshots taken once at submit, never recomputed afterward.
            builder.Property(r => r.CatalogPrice).HasColumnType("decimal(18,2)");
            builder.Property(r => r.Discount).HasColumnType("decimal(18,2)");
            builder.Property(r => r.FinalPrice).HasColumnType("decimal(18,2)");
            builder.Property(r => r.OwnerSalesAgentId).HasMaxLength(450);

            builder.Property(r => r.Status)
             .HasConversion<int>() // store enum as int
             .IsRequired();

            builder.Property(r => r.AdminNote).HasMaxLength(2000);

            // §3 / §6.3 — at most ONE active reservation per unit. A filtered
            // unique index is SQL Server's equivalent of a partial index, the same
            // device already used for IX_PaymentSchedules_ActivePerReservation.
            //
            // This is the concurrency backstop: the availability check in
            // CreateReservationHandler / ResubmitReservationHandler is a
            // read-then-write and cannot survive two simultaneous submits on its
            // own. A lost race surfaces as SQL error 2601/2627, which
            // ApiExceptionFilter translates to 409 UNIT_NOT_AVAILABLE by matching
            // this index name — so the name is part of the contract, not decoration.
            //
            // The filter is written in the PERSISTED representation: Status is
            // stored as int (HasConversion<int> below), so the blocking set
            // SUBMITTED / APPROVED / CONVERTED / CHANGES_REQUESTED is (0, 1, 4, 6).
            // DRAFT (5) is deliberately excluded — a draft must not block a unit
            // (§5.3). Must stay in sync with ReservationStateMachine.UnitBlockingStatuses.
            // Kept alongside the filtered one: the unique index only covers active
            // rows, so lookups over a unit's closed/draft reservations still need
            // a plain FK index.
            builder.HasIndex(r => r.UnitId, "IX_Reservations_UnitId");

            builder.HasIndex(r => r.UnitId, "IX_Reservations_ActivePerUnit")
                   .IsUnique()
                   .HasFilter("[Status] IN (0, 1, 4, 6)");

            builder.HasOne(r => r.Unit)
                .WithMany()
                .HasForeignKey(r => r.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            // §1.1 — the buyer's identity. Nullable while legacy rows are
            // backfilled, and Restrict on delete because §9 forbids removing a
            // person who is attached to a reservation: they are archived instead.
            builder.HasOne(r => r.PrimaryContact)
                   .WithMany()
                   .HasForeignKey(r => r.PrimaryContactId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(r => r.PrimaryContactId, "IX_Reservations_PrimaryContactId");
           


            builder.HasMany(r => r.Documents)
                   .WithOne(d => d.Reservation)
                   .HasForeignKey(d => d.ReservationId)
                   .OnDelete(DeleteBehavior.Cascade);

            // §31.6 — real optimistic concurrency: a stale write throws
            // DbUpdateConcurrencyException, translated to 409
            // RESOURCE_VERSION_CONFLICT by ApiExceptionFilter.
            builder.Property(r => r.RowVersion).IsRowVersion();
        }
    }
}
