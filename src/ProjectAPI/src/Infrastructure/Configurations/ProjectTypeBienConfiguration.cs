using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

/// <summary>
/// Configuration class for the <see cref="ProjectTypeBien"/> entity.
/// </summary>
public class ProjectTypeBienConfiguration : IEntityTypeConfiguration<ProjectTypeBien>
{
    public void Configure(EntityTypeBuilder<ProjectTypeBien> builder)
    {
        // Table name
        builder.ToTable("ProjectTypeBiens");

        // Primary key
        builder.HasKey(ptb => ptb.Id);

        // Properties
        builder.Property(ptb => ptb.Id)
               .ValueGeneratedNever();

        // Relationship with Project
        builder.HasOne(ptb => ptb.Project)
               .WithMany(p => p.TypeBiens)
               .HasForeignKey(ptb => ptb.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        // Relationship with TypeBien
        builder.HasOne(ptb => ptb.TypeBien)
               .WithMany(tb => tb.Projects)
               .HasForeignKey(ptb => ptb.TypeBienId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}