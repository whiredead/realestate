using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="Unit"/> entity.
    /// </summary>
    public class UnitConfiguration : IEntityTypeConfiguration<Unit>
    {
        /// <summary>
        /// Configures the properties and relationships of the <see cref="Unit"/> entity.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity.</param>
        public void Configure(EntityTypeBuilder<Unit> builder)
        {
            builder.ToTable("Units");

            // Defines the primary key for Unit.
            builder.HasKey(unit => unit.Id);

            // Configures the UnitNumber property.
            builder.Property(unit => unit.UnitNumber)
                .HasMaxLength(50);

            // Configures the NumberOfBedrooms property.
            builder.Property(unit => unit.NumberOfBedrooms);

            // Configures the NumberOfBathrooms property.
            builder.Property(unit => unit.NumberOfBathrooms);

            // Configures the ApartmentSurface property.
            builder.Property(unit => unit.ApartmentSurface);

            // Configures the BalconySurface property.
            builder.Property(unit => unit.BalconySurface);

            // Configures the TerraceSurface property.
            builder.Property(unit => unit.TerraceSurface);

            // Configures the GardenSurface property.
            builder.Property(unit => unit.GardenSurface);

            // Configures the View property.
            builder.Property(unit => unit.View)
                .HasMaxLength(150);

            // Configures the Orientation property.
            builder.Property(unit => unit.Orientation)
                .HasMaxLength(100);

            // Configures the TotalSurface property.
            builder.Property(unit => unit.TotalSurface);

            // Configures the SaleableValue property.
            builder.Property(unit => unit.SaleableValue);

            // Configures the SaleableValue1 property.
            builder.Property(unit => unit.SaleableValue1);

            // Configures the PriceSaleableValue property.
            builder.Property(unit => unit.PriceSaleableValue);

            // Configures the PriceSaleableValue1 property.
            builder.Property(unit => unit.PriceSaleableValue1);

            // Configures the LatestPrice property.
            builder.Property(unit => unit.LatestPrice);

            // Comma-delimited photo URLs for this unit — same storage
            // convention as Immeuble.Images (a plain string, not a value
            // converter list), since CreateImmeubleHandler/CreateProjectHandler
            // already pass Images through as-is from the command.
            builder.Property(unit => unit.Images)
                .HasMaxLength(4000);

            // §3 / §6.1 — the commercial status is stored as the canonical
            // UPPER_SNAKE_CASE code in a varchar, guarded by a CHECK constraint
            // (the spec explicitly rules out database enums). The value domain is
            // owned by UnitStatusCodes, so the constraint is generated from it and
            // cannot drift from the enum.
            builder.Property(unit => unit.Status)
                .HasConversion(
                    status => status.ToCode(),
                    stored => UnitStatusCodes.Parse(stored))
                .HasMaxLength(30)
                .IsRequired();

            builder.ToTable(t => t.HasCheckConstraint(
                "CK_Units_Status",
                $"[Status] IN ({string.Join(", ", UnitStatusCodes.All.Select(c => $"'{c}'"))})"));

            builder.HasMany(unit => unit.StatusHistory)
                .WithOne(history => history.Unit)
                .HasForeignKey(history => history.UnitId)
                // Never cascade-delete an audit trail (§6.3).
                .OnDelete(DeleteBehavior.NoAction);

            // Configures the relationship between Unit and Project.
            builder.HasOne(unit => unit.Immeuble)
                .WithMany(project => project.Units)
                .HasForeignKey(unit => unit.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configures the relationship between Unit and its Floor.
            builder.HasOne(unit => unit.Floor)
                .WithMany(f => f.Units)
                .HasForeignKey(unit => unit.FloorId)
                // A floor with units cannot be deleted out from under them —
                // matches the "never destructively touch existing rows" rule
                // the import commit path already follows for buildings/units.
                .OnDelete(DeleteBehavior.Restrict);

            // Configures the relationship between Unit and PropertyDeliveries.
            builder.HasMany(unit => unit.PropertyDeliveries)
                .WithOne(delivery => delivery.Unit)
                .HasForeignKey(delivery => delivery.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            // §7.2 — the référentiel layout this unit realises. Optional: rows
            // that predate the column keep NULL and stay on the bedroom-count
            // fallback, so adding this breaks no existing stock.
            //
            // SET NULL rather than Cascade: retiring a type from the
            // référentiel must never delete the flats that were built to it.
            builder.HasOne(unit => unit.TypeBien)
                .WithMany(type => type.Units)
                .HasForeignKey(unit => unit.TypeBienId)
                .OnDelete(DeleteBehavior.SetNull);

            // The catalogue groups a project's stock by type, so the lookup is
            // always "units of this type", never a scan by id.
            builder.HasIndex(unit => unit.TypeBienId)
                   .HasDatabaseName("IX_Units_TypeBienId");
        }
    }

    /// <summary>Append-only unit status trail (§7). Indexed for chronological reads.</summary>
    public class UnitStatusHistoryConfiguration : IEntityTypeConfiguration<UnitStatusHistory>
    {
        public void Configure(EntityTypeBuilder<UnitStatusHistory> builder)
        {
            builder.ToTable("UnitStatusHistories");
            builder.HasKey(h => h.Id);

            builder.Property(h => h.FromStatus)
                .HasConversion(
                    status => status.HasValue ? status.Value.ToCode() : null,
                    stored => stored == null ? null : UnitStatusCodes.Parse(stored))
                .HasMaxLength(30);

            builder.Property(h => h.ToStatus)
                .HasConversion(
                    status => status.ToCode(),
                    stored => UnitStatusCodes.Parse(stored))
                .HasMaxLength(30)
                .IsRequired();

            builder.Property(h => h.Cause).HasMaxLength(100).IsRequired();
            builder.Property(h => h.Reason).HasMaxLength(2000);
            builder.Property(h => h.ActorUserId).HasMaxLength(450);

            builder.HasIndex(h => new { h.UnitId, h.OccurredAt })
                   .HasDatabaseName("IX_UnitStatusHistories_UnitDate");
        }
    }
}
