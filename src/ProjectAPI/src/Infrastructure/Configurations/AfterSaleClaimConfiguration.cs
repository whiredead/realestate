using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Sales.Entities;

public class AfterSaleClaimConfiguration : IEntityTypeConfiguration<AfterSaleClaim>
{
    public void Configure(EntityTypeBuilder<AfterSaleClaim> b)
    {
        b.ToTable("AfterSaleClaims");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).IsRequired().HasMaxLength(4000);

        b.Property(x => x.Category).HasConversion<int>().IsRequired();
        b.Property(x => x.Priority).HasConversion<int>().IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();

        b.Property(x => x.GuestName).HasMaxLength(150);
        b.Property(x => x.GuestEmail).HasMaxLength(150);
        b.Property(x => x.GuestPhone).HasMaxLength(30);

        b.HasIndex(x => new { x.UnitId, x.Status });
        b.HasIndex(x => x.BuyerId);
        b.HasIndex(x => x.AssignedAgentId);

        b.HasMany(x => x.Attachments)
            .WithOne(a => a.Claim)
            .HasForeignKey(a => a.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Comments)
            .WithOne(c => c.Claim)
            .HasForeignKey(c => c.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.History)
            .WithOne(h => h.Claim)
            .HasForeignKey(h => h.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}



