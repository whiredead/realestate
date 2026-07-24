using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class ImmeublePlanInterieurConfiguration : IEntityTypeConfiguration<ImmeublePlanInterieur>
{
    public void Configure(EntityTypeBuilder<ImmeublePlanInterieur> builder)
    {
        // Configure Project entity

        builder.ToTable("ImmeublePlanInterieurs");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
              .HasDefaultValueSql("NEWID()")
              .ValueGeneratedOnAdd();

        builder.Property(p => p.PhotoLinks).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        builder.HasOne(pi=>pi.Immeuble)
            .WithMany(i=>i.PlanInterieurs)
            .HasForeignKey(pi => pi.ImmeubleId).IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

    }
}

