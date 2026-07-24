using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration for the EspaceTempsReel entity.
    /// </summary>
    public class EspaceTempsReelConfiguration : IEntityTypeConfiguration<EspaceTempsReel>
    {
        public void Configure(EntityTypeBuilder<EspaceTempsReel> builder)
        {
            builder.ToTable("EspaceTempsReel");

            builder.HasKey(et => et.Id);

            builder.Property(et => et.VideoLink)
                   .IsRequired()
                   .HasMaxLength(500); // adjust as needed

            builder.Property(et => et.InsertedAt)
                   .IsRequired();

            builder.HasOne(et => et.Project)
                   .WithMany(p => p.Videos)
                   .HasForeignKey(et => et.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
