namespace ProjectAPI.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Users.Entities;

public class WeeklyAvailabilityConfiguration
    : IEntityTypeConfiguration<WeeklyAvailability>
{
    public void Configure(EntityTypeBuilder<WeeklyAvailability> b)
    {
        b.ToTable("NotaryWeeklyAvailabilities");
        b.HasKey(x => x.Id);

        b.Property(x => x.DayOfWeek).IsRequired();
        b.Property(x => x.StartTime).IsRequired();
        b.Property(x => x.EndTime).IsRequired();

        b.HasOne(x => x.Notary)
         .WithMany(n => n.WeeklyAvailabilities)
         .HasForeignKey(x => x.NotaryId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
