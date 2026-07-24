using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Sales.Entities;


namespace ProjectAPI.Infrastructure.Configurations;
public class ClaimCommentConfiguration : IEntityTypeConfiguration<ClaimComment>
{
    public void Configure(EntityTypeBuilder<ClaimComment> b)
    {
        b.ToTable("ClaimComments");
        b.HasKey(x => x.Id);
        b.Property(x => x.AuthorUserId).IsRequired().HasMaxLength(450);
        b.Property(x => x.Message).IsRequired().HasMaxLength(2000);
        b.HasIndex(x => x.ClaimId);
    }
}