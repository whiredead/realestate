using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration class for the <see cref="TypeBien"/> entity.
    /// </summary>
    public class TypeBienConfiguration : IEntityTypeConfiguration<TypeBien>
    {
        public void Configure(EntityTypeBuilder<TypeBien> builder)
        {
            // Set the table name
            builder.ToTable("TypeBiens");

            // Primary key configuration with identity generation
            builder.HasKey(tb => tb.Id);
            builder.Property(tb => tb.Id)
                   .ValueGeneratedOnAdd();

            // Configure the Name property
            builder.Property(tb => tb.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            // Configure the Description property (optional)
            builder.Property(tb => tb.Description)
                   .HasMaxLength(1000)
                   .IsRequired(false);

            // Configure the Price property (optional, stored as float)
            builder.Property(tb => tb.Price)
                   .HasColumnType("float")
                   .IsRequired(false);

            // Configure the NbrChambre property (optional)
            builder.Property(tb => tb.NbrChambre)
                   .IsRequired(false);

            // Configure the NbrSalleDeBain property (optional)
            builder.Property(tb => tb.NbrSalleDeBain)
                   .IsRequired(false);

            // Shower rooms, counted apart from bathrooms (optional).
            builder.Property(tb => tb.NbrDouche)
                   .IsRequired(false);

            // Parking spaces included with the layout (optional).
            builder.Property(tb => tb.NbrParking)
                   .IsRequired(false);

            // 3D tour for this layout (optional). Same 2048 ceiling MediaUrlPolicy
            // enforces, so a link it accepts always fits the column.
            builder.Property(tb => tb.Module3DLink)
                   .HasMaxLength(2048)
                   .IsRequired(false);

            // Configure the Surface property (optional)
            builder.Property(tb => tb.MinSurface)
                   .IsRequired(false);
            // Configure the MaxSurface property (optional)
            builder.Property(tb => tb.MaxSurface)
                   .IsRequired(false);

            // Configure the ImagesInterieur property (optional)
            builder.Property(tb => tb.ImagesInterieur)
                   .HasMaxLength(500)
                   .IsRequired(false);

            // Configure the Image property (optional)
            builder.Property(tb => tb.Image)
                   .HasMaxLength(500)
                   .IsRequired(false);

            // Relationship with the bridge table ProjectTypeBien
            builder.HasMany(tb => tb.Projects)
                   .WithOne(ptb => ptb.TypeBien)
                   .HasForeignKey(ptb => ptb.TypeBienId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
