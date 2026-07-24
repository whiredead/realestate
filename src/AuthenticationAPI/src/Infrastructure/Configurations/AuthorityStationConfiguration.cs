using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthenticationAPI.Infrastructure.Configurations;

/// <summary>
/// Configures the AuthorityStation entity.
/// </summary>
public class AuthorityStationConfiguration : IEntityTypeConfiguration<AuthorityStation>
{
    /// <summary>
    /// Configures the entity type for AuthorityStation.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<AuthorityStation> builder)
    {
        builder.ToTable("AuthorityStation");

        // Defines the primary key for AuthorityStation.
        builder.HasKey(ast => ast.Id);

        // Configures the NameFr property to be required and have a maximum length of 100 characters.
        builder.Property(ast => ast.NameFr)
            .IsRequired()
            .HasMaxLength(50);

        // Configures the NameAr property to be required and have a maximum length of 100 characters.
        builder.Property(ast => ast.NameAr)
            .IsRequired()
            .HasMaxLength(50);

        // Defines the one-to-many relationship between AuthorityStation and AuthorityStationBch.
        builder.HasMany(ast => ast.AuthorityStationBchs)
            .WithOne(asb => asb.AuthorityStation)
            .HasForeignKey(asb => asb.AuthorityStationId);
    }
}
