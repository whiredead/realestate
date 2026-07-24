using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationAPI.Infrastructure.Configurations
{
    public class PerformanceIndicatorConfiguration : IEntityTypeConfiguration<PerformanceIndicator>
    {
        public void Configure(EntityTypeBuilder<PerformanceIndicator> builder)
        {
            builder.ToTable("PerformanceIndicators");
            builder.HasKey(pi => pi.Id);

            builder.Property(pi => pi.AgentId)
                   .IsRequired()
                   .HasMaxLength(450);

            builder.Property(pi => pi.LeadsGenerated)
                   .IsRequired();

            builder.Property(pi => pi.AppointmentsScheduled)
                   .IsRequired();

            builder.Property(pi => pi.SuccessfulSales)
                   .IsRequired();

            builder.Property(pi => pi.RecordedAt)
                   .IsRequired();
        }
    }
}
