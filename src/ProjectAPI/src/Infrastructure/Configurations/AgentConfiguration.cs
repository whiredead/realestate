using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
      /*  builder.ToTable("Agents");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.About).HasMaxLength(500);
        builder.Property(a => a.Rating).HasDefaultValue(0);

        builder.HasMany(a => a.Projects)
            .WithOne(p => p.Agent)
            .HasForeignKey(p => p.AgentId);

        builder.HasMany(a => a.Appointments)
            .WithOne(ap => ap.Agent)
            .HasForeignKey(ap => ap.AgentId);

        builder.HasMany(a => a.PerformanceIndicators)
            .WithOne(pi => pi.Agent)
            .HasForeignKey(pi => pi.AgentId);*/
    }
}
