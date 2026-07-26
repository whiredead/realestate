using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Handovers.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

/// <summary>
/// Handover persistence (§5.8). Statuses are stored as int to match the
/// convention the final-visit appointments already use in this database; the
/// canonical §-codes live in the *Codes classes and are applied at the wire
/// boundary, exactly as NotaryAppointmentOutcome does.
/// </summary>
public class HandoverAppointmentConfiguration : IEntityTypeConfiguration<HandoverAppointment>
{
    public void Configure(EntityTypeBuilder<HandoverAppointment> builder)
    {
        builder.ToTable("HandoverAppointments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Location).HasMaxLength(300);
        builder.Property(a => a.SalesAgentId).HasMaxLength(450);
        builder.Property(a => a.CreatedBy).HasMaxLength(450);
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasIndex(a => a.ReservationId, "IX_HandoverAppointments_ReservationId");
        builder.HasIndex(a => a.UnitId, "IX_HandoverAppointments_UnitId");

        // §5.8 — at most one live handover per dossier. Terminal attempts
        // (Rejected 3 / Cancelled 4 / Completed 5 / NoShow 6) are excluded so a
        // failed handover can be retried with a fresh appointment.
        builder.HasIndex(a => a.ReservationId, "UX_HandoverAppointments_ActivePerReservation")
               .IsUnique()
               .HasFilter("[Status] IN (0, 1, 2)");

        builder.HasMany(a => a.Reports)
               .WithOne(r => r.Appointment)
               .HasForeignKey(r => r.AppointmentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HandoverReportConfiguration : IEntityTypeConfiguration<HandoverReport>
{
    public void Configure(EntityTypeBuilder<HandoverReport> builder)
    {
        builder.ToTable("HandoverReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Participants).HasMaxLength(1000);
        builder.Property(r => r.Observations).HasMaxLength(4000);

        // One version number per appointment: a correction is a new version, never
        // an edit of a document the buyer already acknowledged (§19.3).
        builder.HasIndex(r => new { r.AppointmentId, r.VersionNo })
               .IsUnique()
               .HasDatabaseName("UX_HandoverReports_AppointmentVersion");

        builder.HasMany(r => r.Items)
               .WithOne(i => i.Report)
               .HasForeignKey(i => i.ReportId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HandoverItemConfiguration : IEntityTypeConfiguration<HandoverItem>
{
    public void Configure(EntityTypeBuilder<HandoverItem> builder)
    {
        builder.ToTable("HandoverItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ItemType).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Comment).HasMaxLength(500);
        builder.ToTable(t => t.HasCheckConstraint("CK_HandoverItems_Quantity", "[Quantity] >= 0"));
    }
}

public class WarrantyConfiguration : IEntityTypeConfiguration<Warranty>
{
    public void Configure(EntityTypeBuilder<Warranty> builder)
    {
        builder.ToTable("Warranties");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.WarrantyTypeCode).HasMaxLength(50).IsRequired();

        builder.HasIndex(w => w.UnitId, "IX_Warranties_UnitId");

        // Delivery is idempotent (§5.8): re-acknowledging the same report must not
        // start a second warranty of the same type on the same unit.
        builder.HasIndex(w => new { w.UnitId, w.WarrantyTypeCode })
               .IsUnique()
               .HasFilter("[IsActive] = 1")
               .HasDatabaseName("UX_Warranties_ActivePerUnitType");

        builder.ToTable(t => t.HasCheckConstraint("CK_Warranties_Period", "[EndsAt] > [StartsAt]"));
    }
}
