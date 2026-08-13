using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

/// <summary>
/// Configuration class for the <see cref="Appointment"/> entity.
/// </summary>
public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    /// <summary>
    /// Configures the properties and relationships of the <see cref="Appointment"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        // Defines the primary key for Appointment.
        builder.HasKey(ap => ap.Id);

        // Configures the Name property with a maximum length of 150 characters.
        builder.Property(ap => ap.Name)
            .HasMaxLength(150);

        // Configures the Email property with a maximum length of 150 characters.
        builder.Property(ap => ap.Email)
            .HasMaxLength(150);

        // Configures the PhoneNumber property with a maximum length of 20 characters.
        builder.Property(ap => ap.PhoneNumber)
            .HasMaxLength(20);

        // Configures the AppointmentDate property to be required.
        builder.Property(ap => ap.AppointmentDate)
            .IsRequired();

        builder.Property(ap => ap.CreatedAt)
            .IsRequired();

        builder.Property(a => a.TypeBienIds)
               .HasConversion(
                   v => string.Join(',', v),
                   v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList());

        // §47.3 shared appointment machine — same vocabulary as final-visit,
        // notary and handover appointments (AppointmentAttemptStatus.ToString()).
        builder.Property(ap => ap.Status).IsRequired().HasMaxLength(30);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Appointments_Status",
            "[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed', 'Rejected', 'Cancelled', 'Completed', 'NoShow', 'Superseded')"));

        builder.Property(a => a.AssignmentSource).HasMaxLength(30);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Appointments_AssignmentSource",
            "[AssignmentSource] IS NULL OR [AssignmentSource] IN ('EXISTING_OWNER', 'ROUND_ROBIN', 'LOWEST_WORKLOAD', 'PRIMARY_AGENT', 'MANUAL_REASSIGNMENT')"));

        builder.Property(a => a.AssignedByUserId).HasMaxLength(450);
        // Not FK'd to AspNetUsers: this is a point-in-time record of who
        // acted, not a live reference — the row must stay meaningful even
        // if that account is later deleted, matching ProjectMembership's
        // own AssignedByUserId (also unconstrained).
        builder.Property(a => a.PreviousSalesAgentId).HasMaxLength(450);
        builder.Property(a => a.ReassignmentReason).HasMaxLength(1000);

        builder.HasOne(ap => ap.Project)
            .WithMany(p => p.Appointments)
            .HasForeignKey(ap => ap.ProjectId);

        // Configures the relationship between Appointment and Agent.
        builder.HasOne(a => a.Agent)
            .WithMany(ag=>ag.Appointments) // No navigation property on AspNetUsers for Appointments
            .HasForeignKey(a => a.SalesAgentId)
            .HasPrincipalKey(u => u.Id) // Maps to AspNetUsers.Id
            .OnDelete(DeleteBehavior.Cascade);

        // §1.1 — every request resolves to one CrmContact, login or not.
        builder.HasOne(a => a.CrmContact)
            .WithMany()
            .HasForeignKey(a => a.CrmContactId)
            .OnDelete(DeleteBehavior.Restrict);

        // §5.1/§10.3 — a retry links back to the appointment it replaces;
        // history is never rewritten in place.
        builder.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(a => a.PreviousAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // §10.1 — no two active (blocking) appointments for the same agent at
        // the exact same instant. Same near-miss caveat as notary appointments:
        // a plain unique index cannot express a +/-1 minute window, so
        // EnsureSlotAvailableAsync in the handler covers that narrower case;
        // this is the DB-level backstop for the exact-instant race.
        builder.HasIndex(a => new { a.SalesAgentId, a.AppointmentDate })
            .IsUnique()
            .HasDatabaseName("UX_Appointments_ActiveSlotPerAgent")
            .HasFilter("[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed') AND [SalesAgentId] IS NOT NULL");
    }
}