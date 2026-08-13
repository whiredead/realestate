using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration for <see cref="ProjectMembership"/> — see the entity's
    /// doc comment for the model this replaces.
    ///
    /// The filtered unique index below enforces "at most one ACTIVE row per
    /// (UserId, ProjectId, RoleCode)" at the database level — the same
    /// technique as IX_Reservations_ActivePerUnit (see
    /// AddActiveReservationPerUnitIndex migration): a SQL Server filtered
    /// index predicate can only reference simple, deterministic comparisons
    /// against literal column values, so it can enforce IsActive = 1
    /// uniqueness but cannot express "no other row whose ValidFrom/ValidUntil
    /// window overlaps this one" (that would need GETUTCDATE() or cross-row
    /// logic, neither of which a static index predicate supports). Guarding
    /// against overlapping validity windows for the same key is therefore the
    /// write path's job (deactivate-then-insert inside one transaction); this
    /// index is the last-resort, race-proof backstop for the IsActive
    /// invariant specifically.
    /// </summary>
    public class ProjectMembershipConfiguration : IEntityTypeConfiguration<ProjectMembership>
    {
        public void Configure(EntityTypeBuilder<ProjectMembership> builder)
        {
            builder.ToTable("ProjectMemberships");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.UserId)
                   .HasMaxLength(450)
                   .IsRequired();

            builder.Property(m => m.RoleCode)
                   .HasMaxLength(30)
                   .IsRequired();

            builder.Property(m => m.AssignedByUserId)
                   .HasMaxLength(450)
                   .IsRequired(false);

            builder.Property(m => m.ValidFrom)
                   .IsRequired();

            builder.Property(m => m.ValidUntil)
                   .IsRequired(false);

            builder.Property(m => m.IsActive)
                   .IsRequired();

            builder.Property(m => m.AssignedAt)
                   .IsRequired();

            builder.HasOne(m => m.Project)
                   .WithMany(p => p.Memberships)
                   .HasForeignKey(m => m.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Agent/Notary/User all share the AspNetUsers table (TPH) — see
            // ProjectAssignmentConfiguration for the same reasoning. Restrict
            // (not Cascade) so deleting a membership never cascades into
            // deleting the account, and to avoid EF's multiple-cascade-path
            // model-build error across the two FKs below that both point
            // into the same table lineage.
            builder.HasOne(m => m.User)
                   .WithMany()
                   .HasForeignKey(m => m.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.AssignedByUser)
                   .WithMany()
                   .HasForeignKey(m => m.AssignedByUserId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Plain lookup index: GetScopedProjectIdsAsync filters by UserId
            // (plus IsActive) on every scoped request.
            builder.HasIndex(m => new { m.UserId, m.IsActive })
                   .HasDatabaseName("IX_ProjectMemberships_UserId_IsActive");

            builder.HasIndex(m => new { m.UserId, m.ProjectId, m.RoleCode })
                   .HasDatabaseName("IX_ProjectMemberships_ActivePerUserProjectRole")
                   .IsUnique()
                   .HasFilter("[IsActive] = 1");
        }
    }
}
