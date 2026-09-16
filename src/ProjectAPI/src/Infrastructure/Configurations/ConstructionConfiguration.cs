using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

/// <summary>Construction milestones — unique code per project, weight 0..100 (§48.7).</summary>
public class ConstructionMilestoneConfiguration : IEntityTypeConfiguration<ConstructionMilestone>
{
    public void Configure(EntityTypeBuilder<ConstructionMilestone> builder)
    {
        builder.ToTable("ConstructionMilestones");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Code).HasMaxLength(50).IsRequired();
        builder.Property(m => m.NameFr).HasMaxLength(200).IsRequired();
        builder.Property(m => m.NameEn).HasMaxLength(200);
        builder.Property(m => m.WeightPercent).HasColumnType("decimal(7,4)");
        builder.Property(m => m.IsValidated).HasDefaultValue(false);

        builder.HasIndex(m => new { m.ProjectId, m.Code })
               .IsUnique()
               .HasDatabaseName("IX_ConstructionMilestones_ProjectCode");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ConstructionMilestones_Weight", "[WeightPercent] >= 0 AND [WeightPercent] <= 100"));
    }
}

/// <summary>Progress updates — versioned, never overwritten (§15.2 FR-CON-004).</summary>
public class ConstructionUpdateConfiguration : IEntityTypeConfiguration<ConstructionUpdate>
{
    public void Configure(EntityTypeBuilder<ConstructionUpdate> builder)
    {
        builder.ToTable("ConstructionUpdates");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.ProgressPercent).HasColumnType("decimal(7,4)");
        builder.Property(u => u.TitleFr).HasMaxLength(200).IsRequired();
        builder.Property(u => u.TitleEn).HasMaxLength(200);

        builder.HasIndex(u => new { u.ProjectId, u.VersionNo })
               .HasDatabaseName("IX_ConstructionUpdates_ProjectVersion");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ConstructionUpdates_Progress", "[ProgressPercent] >= 0 AND [ProgressPercent] <= 100"));
    }
}

/// <summary>Title state — exactly one current row per unit (§48.7).</summary>
public class UnitTitleStateConfiguration : IEntityTypeConfiguration<UnitTitleState>
{
    public void Configure(EntityTypeBuilder<UnitTitleState> builder)
    {
        builder.ToTable("UnitTitleStates");
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.UnitId)
               .IsUnique()
               .HasDatabaseName("IX_UnitTitleStates_Unit");
    }
}

/// <summary>Title history — append-only (§48.7).</summary>
public class UnitTitleHistoryConfiguration : IEntityTypeConfiguration<UnitTitleHistory>
{
    public void Configure(EntityTypeBuilder<UnitTitleHistory> builder)
    {
        builder.ToTable("UnitTitleHistories");
        builder.HasKey(h => h.Id);

        builder.HasIndex(h => new { h.UnitId, h.OccurredAt })
               .HasDatabaseName("IX_UnitTitleHistories_UnitDate");
    }
}

/// <summary>Final-visit case — one per reservation (§48.8).</summary>
public class FinalVisitCaseConfiguration : IEntityTypeConfiguration<FinalVisitCase>
{
    public void Configure(EntityTypeBuilder<FinalVisitCase> builder)
    {
        builder.ToTable("FinalVisitCases");
        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.ReservationId)
               .IsUnique()
               .HasDatabaseName("IX_FinalVisitCases_Reservation");

        builder.HasMany(c => c.Appointments)
               .WithOne(a => a.Case)
               .HasForeignKey(a => a.CaseId)
               .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>Visit attempts — unique attempt number per case (§48.8).</summary>
public class FinalVisitAppointmentConfiguration : IEntityTypeConfiguration<FinalVisitAppointment>
{
    public void Configure(EntityTypeBuilder<FinalVisitAppointment> builder)
    {
        builder.ToTable("FinalVisitAppointments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CauseType).HasMaxLength(100);
        builder.Property(a => a.CauseDescription).HasMaxLength(1000);

        builder.HasIndex(a => new { a.CaseId, a.AttemptNo })
               .IsUnique()
               .HasDatabaseName("IX_FinalVisitAppointments_CaseAttempt");

        // Supports the overlap check for an agent's slots (ADR-0002).
        builder.HasIndex(a => new { a.StartsAt, a.EndsAt })
               .HasDatabaseName("IX_FinalVisitAppointments_Interval");
    }
}

/// <summary>Reports — one version row per appointment version (§48.8).</summary>
public class FinalVisitReportConfiguration : IEntityTypeConfiguration<FinalVisitReport>
{
    public void Configure(EntityTypeBuilder<FinalVisitReport> builder)
    {
        builder.ToTable("FinalVisitReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.GeneralCondition).HasMaxLength(500);
        builder.Property(r => r.Observations).HasMaxLength(4000);
        builder.Property(r => r.DisputeReason).HasMaxLength(1000);

        builder.HasIndex(r => new { r.AppointmentId, r.VersionNo })
               .IsUnique()
               .HasDatabaseName("IX_FinalVisitReports_AppointmentVersion");

        builder.HasMany(r => r.Snags)
               .WithOne(s => s.Report)
               .HasForeignKey(s => s.ReportId)
               .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>Snags — unique code, indexed by severity/status for eligibility (§48.8).</summary>
public class SnagConfiguration : IEntityTypeConfiguration<Snag>
{
    public void Configure(EntityTypeBuilder<Snag> builder)
    {
        builder.ToTable("Snags");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.Property(s => s.CategoryCode).HasMaxLength(50);
        builder.Property(s => s.Description).HasMaxLength(2000).IsRequired();
        builder.Property(s => s.Location).HasMaxLength(200);
        builder.Property(s => s.ResolutionComment).HasMaxLength(2000);

        builder.HasIndex(s => s.Code)
               .IsUnique()
               .HasDatabaseName("IX_Snags_Code");

        // The eligibility calculation filters on severity + status.
        builder.HasIndex(s => new { s.ReportId, s.Severity, s.Status })
               .HasDatabaseName("IX_Snags_ReportSeverityStatus");

        // §31.6 — real optimistic concurrency (see Reservation.RowVersion for rationale).
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}

/// <summary>Snag history — append-only (§48.8).</summary>
public class SnagHistoryConfiguration : IEntityTypeConfiguration<SnagHistory>
{
    public void Configure(EntityTypeBuilder<SnagHistory> builder)
    {
        builder.ToTable("SnagHistories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Comment).HasMaxLength(1000);

        builder.HasOne(h => h.Snag)
               .WithMany()
               .HasForeignKey(h => h.SnagId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(h => new { h.SnagId, h.OccurredAt })
               .HasDatabaseName("IX_SnagHistories_SnagDate");
    }
}
