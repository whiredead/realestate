using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Appointments.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class NotaryAppointmentConfiguration : IEntityTypeConfiguration<NotaryAppointment>
{
    public void Configure(EntityTypeBuilder<NotaryAppointment> builder)
    {
        builder.ToTable("NotaryAppointments");

        builder.HasKey(na => na.Id);

        builder.Property(na => na.BuyerId).HasMaxLength(450);
        builder.Property(na => na.NotaireId).HasMaxLength(450);
        builder.Property(na => na.AgentId).HasMaxLength(450);
        builder.Property(na => na.ConnectedUserId).HasMaxLength(450);
        builder.Property(na => na.Status).IsRequired().HasMaxLength(50);

        // §5.7 — the outcome is a distinct column from the status, stored as its
        // canonical code (§6.1: varchar + CHECK, not a database enum). Nullable:
        // an appointment has no outcome until it completes.
        builder.Property(na => na.Outcome)
            .HasConversion(
                outcome => outcome.HasValue ? outcome.Value.ToCode() : null,
                stored => stored == null ? null : NotaryOutcomeCodes.Parse(stored))
            .HasMaxLength(30);

        builder.Property(na => na.OutcomeRecordedBy).HasMaxLength(450);
        builder.Property(na => na.OutcomeNote).HasMaxLength(2000);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_NotaryAppointments_Outcome",
            $"[Outcome] IS NULL OR [Outcome] IN ({string.Join(", ", NotaryOutcomeCodes.All.Select(c => $"'{c}'"))})"));

        builder.Property(na => na.CreatedAt).IsRequired();
        builder.Property(na => na.PreviousNotaireId).HasMaxLength(450);
        builder.Property(na => na.ReassignmentReason).HasMaxLength(1000);

        // §5.1/§10.3 — a retry/reassignment links back to the appointment it
        // replaces; history is never rewritten in place. Mirrors Appointment's
        // own PreviousAppointmentId FK.
        builder.HasOne<NotaryAppointment>()
            .WithMany()
            .HasForeignKey(na => na.PreviousAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // DB-level backstop for the same-instant race CreateNotaryAppointmentHandler
        // guards against in application code (EnsureSlotAvailableAsync). The app
        // check additionally rejects anything within +/-1 minute of an existing
        // slot; a plain unique index cannot express that window (it would need an
        // exclusion constraint, which SQL Server does not support), so near-miss
        // overlaps remain enforced only by the application check. This index closes
        // the narrower but more likely case: two concurrent requests booking the
        // exact same instant for the same notary.
        builder.HasIndex(na => new { na.NotaireId, na.AppointmentDate })
            .IsUnique()
            .HasDatabaseName("UX_NotaryAppointments_ActiveSlotPerNotary")
            .HasFilter("[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed')");

        builder.Property(na => na.PropertyPrice).HasColumnType("decimal(18,2)");
        builder.Property(na => na.TaxFees).HasColumnType("decimal(18,2)");
        builder.Property(na => na.TahfidFees).HasColumnType("decimal(18,2)");

        builder.HasOne(na => na.Reservation)
            .WithMany()
            .HasForeignKey(na => na.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
         .HasOne(n => n.Notary)
         .WithMany(u => u.NotaryAppointments)
         .HasForeignKey(n => n.NotaireId)
         // CHANGE THIS:
         // .OnDelete(DeleteBehavior.Cascade);
         // TO THIS:
         .OnDelete(DeleteBehavior.Restrict);
    }
}
