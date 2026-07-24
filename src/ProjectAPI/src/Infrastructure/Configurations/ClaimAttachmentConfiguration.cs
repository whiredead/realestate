using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class ClaimAttachmentConfiguration : IEntityTypeConfiguration<ClaimAttachment>
{
    public void Configure(EntityTypeBuilder<ClaimAttachment> b)
    {
        b.ToTable("ClaimAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Url).IsRequired().HasMaxLength(2000);
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(200);
        b.HasIndex(x => x.ClaimId);
    }
}
