using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="ImmeubleTypeBien"/> entity.
    /// </summary>
    public class ImmeubleTypeBienConfiguration : IEntityTypeConfiguration<ImmeubleTypeBien>
    {
        public void Configure(EntityTypeBuilder<ImmeubleTypeBien> builder)
        {
            // Table name
            builder.ToTable("ImmeubleTypeBiens");

            // Primary key
            builder.HasKey(itb => itb.Id);

            // Properties
            builder.Property(itb => itb.Id)
                   .ValueGeneratedNever();

            // Relationship with Immeuble
            builder.HasOne(itb => itb.Immeuble)
                   .WithMany(i => i.TypeBiens)
                   .HasForeignKey(itb => itb.ImmeubleId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Relationship with TypeBien
            builder.HasOne(itb => itb.TypeBien)
                   .WithMany(tb => tb.Immeubles)
                   .HasForeignKey(itb => itb.TypeBienId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
