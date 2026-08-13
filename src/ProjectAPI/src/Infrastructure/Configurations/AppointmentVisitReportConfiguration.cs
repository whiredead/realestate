using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Appointments.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AppointmentVisitReportConfiguration : IEntityTypeConfiguration<AppointmentVisitReport>
{
    public void Configure(EntityTypeBuilder<AppointmentVisitReport> builder)
    {
        builder.ToTable("AppointmentVisitReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PropertiesPresentedUnitIds)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList());

        builder.Property(r => r.InterestLevel).IsRequired().HasMaxLength(20);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AppointmentVisitReports_InterestLevel",
            "[InterestLevel] IN ('LOW', 'MEDIUM', 'HIGH', 'VERY_HIGH')"));

        builder.Property(r => r.VisitResult).IsRequired().HasMaxLength(30);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AppointmentVisitReports_VisitResult",
            "[VisitResult] IN ('INTERESTED_FOLLOW_UP', 'READY_TO_RESERVE', 'NOT_INTERESTED', 'NO_SHOW', 'RESCHEDULE_NEEDED')"));

        builder.Property(r => r.ConfirmedBudget).HasColumnType("decimal(18,2)");
        builder.Property(r => r.ConfirmedRequirements).HasMaxLength(2000);
        builder.Property(r => r.Objections).HasMaxLength(2000);
        builder.Property(r => r.NextAction).HasMaxLength(1000);
        builder.Property(r => r.InternalNotes).HasMaxLength(4000);
        builder.Property(r => r.AuthorUserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasOne(r => r.Appointment)
            .WithMany(a => a.VisitReports)
            .HasForeignKey(r => r.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.AppointmentId);
    }
}
