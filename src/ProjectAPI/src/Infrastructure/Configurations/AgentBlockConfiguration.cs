using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AgentBlockConfiguration
    : IEntityTypeConfiguration<AgentBlock>
{
    public void Configure(EntityTypeBuilder<AgentBlock> b)
    {
        b.ToTable("AgentBlocks");
        b.HasKey(x => x.Id);

        b.Property(x => x.Start).IsRequired();
        b.Property(x => x.End).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired(false);

        b.HasOne(x => x.Agent)
         .WithMany(a => a.Blocks)
         .HasForeignKey(x => x.AgentId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
