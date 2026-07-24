using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Sales.Entities;


namespace ProjectAPI.Infrastructure.Configurations;

public class ClaimHistoryConfiguration : IEntityTypeConfiguration<ClaimHistory>
{
    public void Configure(EntityTypeBuilder<ClaimHistory> b)
    {
        b.ToTable("ClaimHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.ChangedByUserId).IsRequired().HasMaxLength(450);
        b.HasIndex(x => x.ClaimId);
        b.Property(x => x.FromStatus).HasConversion<int>();
        b.Property(x => x.ToStatus).HasConversion<int>();
    }
}
