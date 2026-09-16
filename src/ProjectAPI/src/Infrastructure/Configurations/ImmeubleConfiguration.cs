using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="Immeuble"/> entity.
    /// </summary>
    public class ImmeubleConfiguration : IEntityTypeConfiguration<Immeuble>
    {
        /// <summary>
        /// Configures the properties and relationships of the <see cref="Immeuble"/> entity.
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity.</param>
        public void Configure(EntityTypeBuilder<Immeuble> builder)
        {
            builder.ToTable("Immeubles");

            // Defines the primary key for Project.
            builder.HasKey(p => p.Id);

            // Configures the Name property to be required.
            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(150);

            // Configures the Location property to be optional.
            builder.Property(p => p.Location)
                .HasMaxLength(250);

            // Configures the Type property to be optional.
            builder.Property(p => p.Type)
                .HasMaxLength(50);

            // Configures the ResidencyType property.
            builder.Property(p => p.ResidencyType)
                .HasMaxLength(100);


            // Configures the Status property to be required.
            builder.Property(p => p.Status)
                .IsRequired()
                .HasMaxLength(50);

            // Configures the Images property to be optional.
            builder.Property(p => p.Images);

            // Configures the Module3DLink property.
            builder.Property(p => p.Module3DLink)
                .HasMaxLength(250);

            // Configures the Description property.
            builder.Property(p => p.Description)
                .HasMaxLength(1000);

            // Configures the Latitude property to be required.
            builder.Property(p => p.Latitude)
                .IsRequired();

            // Configures the Longitude property to be required.
            builder.Property(p => p.Longitude)
                .IsRequired();

            // Configures the NumberOfUnits property.
            builder.Property(p => p.NumberOfUnits)
                .IsRequired();

            // Configures the NumberOfSoldUnites property.
            builder.Property(p => p.NumberOfSoldUnites)
                .IsRequired();

            // Configures the NumberOfAvailableUnites property.
            builder.Property(p => p.NumberOfAvailableUnites)
                .IsRequired();

            // Configures the SellsPercentage property.
            builder.Property(p => p.SellsPercentage)
                .IsRequired();

            // Configures the MinSellableSurfaceRange property.
            builder.Property(p => p.MinSellableSurfaceRange)
                .IsRequired();

            // Configures the MaxSellableSurfaceRange property.
            builder.Property(p => p.MaxSellableSurfaceRange)
                .IsRequired();

            // Configures the relationships between Project and Assignments.
            builder.HasMany(p => p.Assignments)
                .WithOne(a => a.Immeuble)
                .HasForeignKey(a => a.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
         

            // Configures the relationships between Project and Units.
            builder.HasMany(p => p.Units)
                .WithOne(u => u.Immeuble)
                .HasForeignKey(u => u.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.Agent)
                .WithMany() // No navigation property on AspNetUsers for Projects
                .HasForeignKey(p => p.AgentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: Project -> Immeuble
            builder.HasOne(i => i.Project)
                  .WithMany(p => p.Immeubles)
                  .HasForeignKey(i => i.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
