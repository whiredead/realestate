using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class QuartierFeatureConfiguration : IEntityTypeConfiguration<QuartierFeature>
{
    public void Configure(EntityTypeBuilder<QuartierFeature> builder)
    {
        builder.ToTable("QuartierFeatures");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Title).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Description).HasMaxLength(2000);
        builder.Property(f => f.Image).HasMaxLength(2000);
        builder.HasIndex(f => new { f.QuartierId, f.SequenceNo });

        // A feature has no meaning without its quartier.
        builder.HasOne<Quartier>()
            .WithMany(q => q.Features)
            .HasForeignKey(f => f.QuartierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
