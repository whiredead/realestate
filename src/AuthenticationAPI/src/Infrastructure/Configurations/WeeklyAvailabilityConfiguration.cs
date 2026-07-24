using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationAPI.Infrastructure.Configurations;

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
    }
}