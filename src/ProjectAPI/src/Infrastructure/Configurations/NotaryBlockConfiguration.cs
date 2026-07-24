using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Users.Entities;


namespace ProjectAPI.Infrastructure.Configurations;

public class NotaryBlockConfiguration
    : IEntityTypeConfiguration<NotaryBlock>
{
    public void Configure(EntityTypeBuilder<NotaryBlock> b)
    {
        b.ToTable("NotaryBlocks");
        b.HasKey(x => x.Id);

        b.Property(x => x.Start).IsRequired();
        b.Property(x => x.End).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired(false);

        b.HasOne(x => x.Notary)
         .WithMany(n => n.Blocks)
         .HasForeignKey(x => x.NotaryId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}