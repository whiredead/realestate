using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AgentAppointmentSettingsConfiguration : IEntityTypeConfiguration<AgentAppointmentSettings>
{
    public void Configure(EntityTypeBuilder<AgentAppointmentSettings> b)
    {
        b.ToTable("AgentAppointmentSettings");
        b.HasKey(x => x.Id);

        b.Property(x => x.AgentId).IsRequired().HasMaxLength(450);
        b.Property(x => x.DurationMinutes).IsRequired();
        b.Property(x => x.BufferMinutes).IsRequired();

        // One settings row per agent.
        b.HasIndex(x => x.AgentId).IsUnique();

        b.HasOne(x => x.Agent)
         .WithOne(a => a.AppointmentSettings)
         .HasForeignKey<AgentAppointmentSettings>(x => x.AgentId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
