using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Appointments.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AppointmentAssignmentHistoryConfiguration : IEntityTypeConfiguration<AppointmentAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentAssignmentHistory> builder)
    {
        builder.ToTable("AppointmentAssignmentHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SalesAgentId).HasMaxLength(450);
        builder.Property(x => x.PreviousSalesAgentId).HasMaxLength(450);
        builder.Property(x => x.ActorUserId).HasMaxLength(450);

        builder.Property(x => x.AssignmentSource).IsRequired().HasMaxLength(30);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AppointmentAssignmentHistories_AssignmentSource",
            "[AssignmentSource] IN ('EXISTING_OWNER', 'ROUND_ROBIN', 'LOWEST_WORKLOAD', 'PRIMARY_AGENT', 'MANUAL_REASSIGNMENT')"));

        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.AssignedAt).IsRequired();

        builder.HasOne(x => x.Appointment)
            .WithMany(a => a.AssignmentHistory)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Append-only: no update path is ever exposed by the application —
        // rows are only ever inserted, never modified or deleted.
        builder.HasIndex(x => x.AppointmentId);
    }
}
