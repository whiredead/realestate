namespace ProjectAPI.Infrastructure.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Immeubles.Entities;

public class ImmeubleFeatureConfiguration : IEntityTypeConfiguration<ImmeubleFeature>
{
    public void Configure(EntityTypeBuilder<ImmeubleFeature> builder)
    {
        builder.ToTable("ImmeubleFeatures");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasOne(f => f.Immeuble)
            .WithMany(i => i.Features)
            .HasForeignKey(f => f.ImmeubleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
