using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AgentWeeklyAvailabilityConfiguration
    : IEntityTypeConfiguration<AgentWeeklyAvailability>
{
    public void Configure(EntityTypeBuilder<AgentWeeklyAvailability> b)
    {
        b.ToTable("AgentWeeklyAvailabilities");
        b.HasKey(x => x.Id);

        b.Property(x => x.DayOfWeek).IsRequired();
        b.Property(x => x.StartTime).IsRequired();
        b.Property(x => x.EndTime).IsRequired();

        b.HasOne(x => x.Agent)
         .WithMany(a => a.WeeklyAvailabilities)
         .HasForeignKey(x => x.AgentId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
