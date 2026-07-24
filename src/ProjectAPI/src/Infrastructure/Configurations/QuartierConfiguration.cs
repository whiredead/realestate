using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="Quartier"/> entity.
    /// </summary>
    public class QuartierConfiguration : IEntityTypeConfiguration<Quartier>
    {
        public void Configure(EntityTypeBuilder<Quartier> builder)
        {
            builder.ToTable("Quartiers");

            builder.HasKey(q => q.Id);

            builder.Property(q => q.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(q => q.Description)
                .HasMaxLength(2000)
                .IsRequired(false);

            builder.Property(q => q.Images)
                .HasMaxLength(20000)
                .IsRequired(false);

            // One-to-many: one Quartier => many Projects
            builder.HasMany(q => q.Projects)
                .WithOne(p => p.Quartier)
                .HasForeignKey(p => p.QuartierId)
                .OnDelete(DeleteBehavior.SetNull);
            // or Cascade, depending on your domain logic
        }
    }
}
