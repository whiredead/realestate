using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.ToTable("Floors");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(50).IsRequired();

        builder.HasOne(f => f.Immeuble)
            .WithMany()
            .HasForeignKey(f => f.ImmeubleId)
            .OnDelete(DeleteBehavior.Cascade);

        // One floor name per building — matches how the import commit path
        // resolves floors (name-matched within a building, never duplicated).
        builder.HasIndex(f => new { f.ImmeubleId, f.Name })
            .IsUnique()
            .HasDatabaseName("UX_Floors_ImmeubleName");
    }
}
