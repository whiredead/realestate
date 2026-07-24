using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        // Configure Project entity

        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Description);
        builder.Property(p => p.Location).HasMaxLength(250);
        builder.Property(p => p.Address).HasMaxLength(250);
        builder.Property(p => p.Images).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        // Relationship with Quartier
        builder.HasOne(p => p.Quartier)
            .WithMany(q => q.Projects)
            .HasForeignKey(p => p.QuartierId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship with TypeBiens through ProjectTypeBien
        builder.HasMany(p => p.TypeBiens)
            .WithOne(ptb => ptb.Project)
            .HasForeignKey(ptb => ptb.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
