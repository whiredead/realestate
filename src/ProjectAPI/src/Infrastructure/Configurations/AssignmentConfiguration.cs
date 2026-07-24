using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

/// <summary>
/// Configuration class for the <see cref="ImmeubleAssignment"/> entity.
/// </summary>
public class AssignmentConfiguration : IEntityTypeConfiguration<ImmeubleAssignment>
{
    /// <summary>
    /// Configures the properties and relationships of the <see cref="ImmeubleAssignment"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<ImmeubleAssignment> builder)
    {
        builder.ToTable("Assignments");

        // Defines the primary key for ProjectAssignment.
        builder.HasKey(a => a.Id);

        // Configures the CreatedAt property to be required.
        builder.Property(a => a.CreatedAt)
            .IsRequired();

        // Configures the IsActive property to be required.
        builder.Property(a => a.IsActive)
            .IsRequired();

        // Configures the relationship between ProjectAssignment and Project.
        builder.HasOne(a => a.Immeuble)
            .WithMany(p => p.Assignments)
            .HasForeignKey(a => a.ProjectId);

        // Configures the relationship between ProjectAssignment and Agent.
        builder.HasOne(a => a.Agent)
            .WithMany()
            .HasForeignKey(a => a.AgentId);
    }
}