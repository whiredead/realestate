using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthenticationAPI.Infrastructure.Configurations;

/// <summary>
/// Configures the SecurityStation entity.
/// </summary>
public class SecurityStationConfiguration : IEntityTypeConfiguration<SecurityStation>
{
    /// <summary>
    /// Configures the entity type for SecurityStation.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<SecurityStation> builder)
    {
        builder.ToTable("SecurityStation");

        // Defines the primary key for SecurityStation.
        builder.HasKey(ast => ast.Id);

        // Configures the NameFr property to be required.
        builder.Property(ast => ast.NameFr)
            .IsRequired()
            .HasMaxLength(50);

        // Configures the NameAr property to be required.
        builder.Property(ast => ast.NameAr)
            .IsRequired()
            .HasMaxLength(50);

        // Defines the one-to-many relationship between SecurityStation and SecurityStationBch.
        builder.HasMany(ast => ast.SecurityStationBchs)
            .WithOne(asb => asb.SecurityStation)
            .HasForeignKey(asb => asb.SecurityStationId);
    }
}
