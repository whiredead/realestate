namespace ProjectAPI.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

public class QuartierAmenityConfiguration : IEntityTypeConfiguration<QuartierAmenity>
{
    public void Configure(EntityTypeBuilder<QuartierAmenity> builder)
    {
        builder.ToTable("QuartierAmenities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(150);
        builder.Property(a => a.Icon).HasMaxLength(50);
        builder.HasIndex(a => a.ProjectId);
    }
}
