using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="ProjectAssignment"/> entity.
    /// This entity represents a many-to-many relationship between a project and an agent.
    /// </summary>
    public class ProjectAssignmentConfiguration : IEntityTypeConfiguration<ProjectAssignment>
    {
        /// <summary>
        /// Configures the properties and relationships of the <see cref="ProjectAssignment"/> entity.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity.</param>
        public void Configure(EntityTypeBuilder<ProjectAssignment> builder)
        {
            // Map the entity to the "ProjectAssignments" table.
            builder.ToTable("ProjectAssignments");

            // Define the primary key.
            builder.HasKey(pa => pa.Id);

            builder.Property(pa => pa.AgentId)
                   .HasMaxLength(450)
                   .IsRequired(false);

            builder.Property(pa => pa.NotaryId)
                   .HasMaxLength(450)
                   .IsRequired(false);
            builder.Property(pa => pa.CreatedAt)
                   .IsRequired();

            // Configure the IsActive property.
            builder.Property(pa => pa.IsActive)
                   .IsRequired();

            // Configure the relationship with the Project entity.
            builder.HasOne(pa => pa.Project)
                   .WithMany(p => p.Assignments)
                   .HasForeignKey(pa => pa.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship with the Agent entity.
            // Since Agent is derived from User (stored in AspNetUsers), we reference it by its AgentId.
            builder.HasOne(pa => pa.Agent)
                   .WithMany(a => a.Assignments)
                   .HasForeignKey(pa => pa.AgentId)
                   .OnDelete(DeleteBehavior.NoAction);

            // Configure the relationship with the Notary entity.
            // Since Notary is derived from User (stored in AspNetUsers), we reference it by its NotaryId.
            builder.HasOne(pa => pa.Notary)
                   .WithMany(a => a.Assignments)
                   .HasForeignKey(pa => pa.NotaryId)
                   .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
