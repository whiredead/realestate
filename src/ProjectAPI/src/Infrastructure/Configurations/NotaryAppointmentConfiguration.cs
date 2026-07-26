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
