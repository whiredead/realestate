using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration for the <see cref="NotaryDateDisponibilite"/> entity.
    /// </summary>
    public class NotaryDateDisponibiliteConfiguration : IEntityTypeConfiguration<NotaryDateDisponibilite>
    {
        public void Configure(EntityTypeBuilder<NotaryDateDisponibilite> builder)
        {
            // Map to table
            builder.ToTable("NotaryDateDisponibilites");

            // Primary key
            builder.HasKey(ndd => ndd.Id);

            // NotaryId FK
            builder.Property(ndd => ndd.NotaryId)
                   .IsRequired()
                   .HasMaxLength(450);

            // StartDate and EndDate
            builder.Property(ndd => ndd.StartDate)
                   .IsRequired();

            builder.Property(ndd => ndd.EndDate)
                   .IsRequired();

            // Status ("Available" / "Unavailable")
            builder.Property(ndd => ndd.Status)
                   .IsRequired()
                   .HasMaxLength(50);

            // DateCreation
            builder.Property(ndd => ndd.DateCreation)
                   .IsRequired();

            // Relationship: each availability belongs to one Notary (stored in AspNetUsers)
            builder.HasOne(ndd => ndd.Notary)
                   .WithMany(n=>n.NotaryDatesDisponibilites)
                   .HasForeignKey(ndd => ndd.NotaryId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
