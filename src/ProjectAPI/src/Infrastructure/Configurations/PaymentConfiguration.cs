using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Payments.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

/// <summary>
/// Schedules — one ACTIVE per reservation (§14.2), amounts in numeric(15,2) (§30.1).
/// </summary>
public class PaymentScheduleConfiguration : IEntityTypeConfiguration<PaymentSchedule>
{
    public void Configure(EntityTypeBuilder<PaymentSchedule> builder)
    {
        builder.ToTable("PaymentSchedules");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ContractAmount).HasColumnType("decimal(15,2)");
        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();
        builder.Property(s => s.VersionNo).IsRequired();

        // Enforces "one ACTIVE schedule per reservation" declaratively:
        // a filtered unique index is SQL Server's equivalent of a partial index
        // (ADR-0002). Status 1 == Active.
        builder.HasIndex(s => s.ReservationId)
               .HasFilter("[Status] = 1")
               .IsUnique()
               .HasDatabaseName("IX_PaymentSchedules_ActivePerReservation");

        builder.HasIndex(s => new { s.ReservationId, s.VersionNo })
               .IsUnique()
               .HasDatabaseName("IX_PaymentSchedules_ReservationVersion");

        builder.HasMany(s => s.Installments)
               .WithOne(i => i.Schedule)
               .HasForeignKey(i => i.ScheduleId)
               .OnDelete(DeleteBehavior.NoAction);

        // §31.6 — real optimistic concurrency (see Reservation.RowVersion for rationale).
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}

/// <summary>Installments — unique sequence per schedule (§48.6).</summary>
public class PaymentInstallmentConfiguration : IEntityTypeConfiguration<PaymentInstallment>
{
    public void Configure(EntityTypeBuilder<PaymentInstallment> builder)
    {
        builder.ToTable("PaymentInstallments");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Percentage).HasColumnType("decimal(7,4)");
        builder.Property(i => i.Amount).HasColumnType("decimal(15,2)");
        builder.Property(i => i.LabelFr).HasMaxLength(200).IsRequired();
        builder.Property(i => i.LabelEn).HasMaxLength(200);
        builder.Property(i => i.Comment).HasMaxLength(1000);

        builder.HasIndex(i => new { i.ScheduleId, i.SequenceNo })
               .IsUnique()
               .HasDatabaseName("IX_PaymentInstallments_ScheduleSequence");

        // Percentages and amounts must stay non-negative (§48.11 CHECK constraints).
        builder.ToTable(t => t.HasCheckConstraint("CK_PaymentInstallments_Percentage", "[Percentage] >= 0 AND [Percentage] <= 100"));
        builder.ToTable(t => t.HasCheckConstraint("CK_PaymentInstallments_Amount", "[Amount] >= 0"));
    }
}

/// <summary>Payments — immutable entries, unique external reference (§14.3).</summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasColumnType("decimal(15,2)");
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.MethodCode).HasMaxLength(50);
        builder.Property(p => p.ExternalReference).HasMaxLength(150);
        builder.Property(p => p.Source).HasMaxLength(30).IsRequired();
        builder.Property(p => p.Comment).HasMaxLength(1000);

        builder.HasIndex(p => p.ReservationId)
               .HasDatabaseName("IX_Payments_Reservation");

        // §14.3: the external reference is unique per source when provided.
        builder.HasIndex(p => new { p.Source, p.ExternalReference })
               .IsUnique()
               .HasFilter("[ExternalReference] IS NOT NULL")
               .HasDatabaseName("IX_Payments_SourceReference");

        // A reversal points at the payment it cancels; never cascade-delete a
        // financial entry (§30.1).
        builder.HasOne<Payment>()
               .WithMany()
               .HasForeignKey(p => p.ReversalOfPaymentId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Allocations)
               .WithOne(a => a.Payment)
               .HasForeignKey(a => a.PaymentId)
               .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>Allocations linking payments to installments (§48.6).</summary>
public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AllocatedAmount).HasColumnType("decimal(15,2)");

        builder.HasOne(a => a.Installment)
               .WithMany(i => i.Allocations)
               .HasForeignKey(a => a.InstallmentId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(a => a.InstallmentId)
               .HasDatabaseName("IX_PaymentAllocations_Installment");
    }
}
