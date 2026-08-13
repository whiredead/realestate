using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Appointments.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class NotaryAppointmentAssignmentHistoryConfiguration : IEntityTypeConfiguration<NotaryAppointmentAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<NotaryAppointmentAssignmentHistory> builder)
    {
        builder.ToTable("NotaryAppointmentAssignmentHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NotaireId).HasMaxLength(450);
        builder.Property(x => x.PreviousNotaireId).HasMaxLength(450);
        builder.Property(x => x.ActorUserId).HasMaxLength(450);

        builder.Property(x => x.AssignmentSource).IsRequired().HasMaxLength(30);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_NotaryAppointmentAssignmentHistories_AssignmentSource",
            "[AssignmentSource] IN ('MANUAL', 'MANUAL_REASSIGNMENT')"));

        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.AssignedAt).IsRequired();

        builder.HasOne(x => x.NotaryAppointment)
            .WithMany(a => a.AssignmentHistory)
            .HasForeignKey(x => x.NotaryAppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.NotaryAppointmentId);
    }
}
