namespace ProjectAPI.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

public class ProjectFeatureConfiguration : IEntityTypeConfiguration<ProjectFeature>
{
    public void Configure(EntityTypeBuilder<ProjectFeature> builder)
    {
        builder.ToTable("ProjectFeatures");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasOne(f => f.Project)
            .WithMany(p => p.Features)
            .HasForeignKey(f => f.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
