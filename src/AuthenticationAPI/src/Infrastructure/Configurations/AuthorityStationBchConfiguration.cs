using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthenticationAPI.Infrastructure.Configurations;

/// <summary>
/// Configures the AuthorityStationBch entity.
/// </summary>
public class AuthorityStationBchConfiguration : IEntityTypeConfiguration<AuthorityStationBch>
{
    /// <summary>
    /// Configures the AuthorityStationBch entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<AuthorityStationBch> builder)
    {
        builder.ToTable("AuthorityStationBch");

        builder.HasKey(asb => new { asb.AuthorityStationId, asb.BchId });

        builder.HasOne(asb => asb.AuthorityStation)
            .WithMany(ast => ast.AuthorityStationBchs)
            .HasForeignKey(asb => asb.AuthorityStationId);

        builder.HasOne(asb => asb.Bch)
            .WithMany(b => b.AuthorityStationBchs)
            .HasForeignKey(asb => asb.BchId);
    }
}
