using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthenticationAPI.Infrastructure.Configurations;

/// <summary>
/// Configures the SecurityStationBch entity.
/// </summary>
public class SecurityStationBchConfiguration : IEntityTypeConfiguration<SecurityStationBch>
{
    /// <summary>
    /// Configures the SecurityStationBch entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<SecurityStationBch> builder)
    {
        builder.ToTable("SecurityStationBch");

        builder.HasKey(asb => new { asb.SecurityStationId, asb.BchId });

        builder.HasOne(asb => asb.SecurityStation)
            .WithMany(ast => ast.SecurityStationBchs)
            .HasForeignKey(asb => asb.SecurityStationId);

        builder.HasOne(asb => asb.Bch)
            .WithMany(b => b.SecurityStationBchs)
            .HasForeignKey(asb => asb.BchId);
    }
}
