using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public sealed class FeatureReferenceConfiguration : IEntityTypeConfiguration<FeatureReference>
{
    public void Configure(EntityTypeBuilder<FeatureReference> builder)
    {
        builder.ToTable("FeatureReferences"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.IconUrl).HasMaxLength(2000);
        builder.Property(x => x.Scope).HasMaxLength(20).IsRequired();
    }
}
