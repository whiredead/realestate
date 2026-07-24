using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthenticationAPI.Infrastructure.Configurations;

/// <summary>
/// Configures the Bch entity.
/// </summary>
public class BchConfiguration : IEntityTypeConfiguration<Bch>
{
    /// <summary>
    /// Configures the entity type for Bch.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<Bch> builder)
    {
        builder.ToTable("Bch");

        // Defines the primary key for Bch.
        builder.HasKey(b => b.Id);

        // Configures the NameAr property to be required and have a maximum length of 50 characters.
        builder.Property(b => b.NameAr)
            .IsRequired()
            .HasMaxLength(50);

        // Configures the NameFr property to be required and have a maximum length of 50 characters.
        builder.Property(b => b.NameFr)
            .IsRequired()
            .HasMaxLength(50);

        // Configures the PhoneNumber property to be required and have a maximum length of 20 characters.
        builder.Property(b => b.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        // Defines the one-to-many relationship between Bch and AuthorityStationBch.
        builder.HasMany(b => b.AuthorityStationBchs)
            .WithOne(asb => asb.Bch)
            .HasForeignKey(asb => asb.BchId);

        // Defines the one-to-many relationship between Bch and SecurityStationBchs.
        builder.HasMany(b => b.SecurityStationBchs)
            .WithOne(asb => asb.Bch)
            .HasForeignKey(asb => asb.BchId);
    }
}
