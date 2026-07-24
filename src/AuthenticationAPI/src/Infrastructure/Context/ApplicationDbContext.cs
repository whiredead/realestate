using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Infrastructure.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Infrastructure.Context;

/// <summary>
/// Represents the application database context, extending IdentityDbContext for User management.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<User, Role, string, IdentityUserClaim<string>, UserRole, IdentityUserLogin<string>, IdentityRoleClaim<string>, IdentityUserToken<string>>
{
    public DbSet<AuthorityStation> AuthorityStations { get; set; }

    public DbSet<AuthorityStationBch> AuthorityStationBchs { get; set; }

    public DbSet<SecurityStation> SecurityStations { get; set; }

    public DbSet<SecurityStationBch> SecurityStationBchs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new BchConfiguration());
        builder.ApplyConfiguration(new UserConfiguration());
        builder.ApplyConfiguration(new UserRoleConfiguration());
        builder.ApplyConfiguration(new AuthorityStationConfiguration());
        builder.ApplyConfiguration(new AuthorityStationBchConfiguration());
        builder.ApplyConfiguration(new SecurityStationConfiguration());
        builder.ApplyConfiguration(new SecurityStationBchConfiguration());
        builder.ApplyConfiguration(new PerformanceIndicatorConfiguration());
        builder.ApplyConfiguration(new WeeklyAvailabilityConfiguration());

    }
    /// <summary>
    /// Constructor for ApplicationDbContext.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
}