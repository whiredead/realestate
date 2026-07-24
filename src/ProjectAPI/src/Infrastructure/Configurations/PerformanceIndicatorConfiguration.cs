using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

public class PerformanceIndicatorConfiguration : IEntityTypeConfiguration<PerformanceIndicator>
{
    public void Configure(EntityTypeBuilder<PerformanceIndicator> builder)
    {
        builder.ToTable("PerformanceIndicators");
        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.LeadsGenerated).IsRequired();
        builder.Property(pi => pi.AppointmentsScheduled).IsRequired();
        builder.Property(pi => pi.SuccessfulSales).IsRequired();
        builder.Property(pi => pi.RecordedAt).IsRequired();

        // Ignore ConversionRate as it is a calculated property
        builder.Ignore(pi => pi.ConversionRate);
    }
}
