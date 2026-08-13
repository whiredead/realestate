using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AgentDateOverrideConfiguration : IEntityTypeConfiguration<AgentDateOverride>
{
    public void Configure(EntityTypeBuilder<AgentDateOverride> builder)
    {
        builder.ToTable("AgentDateOverrides");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.AgentId)
               .IsRequired()
               .HasMaxLength(450);

        builder.Property(o => o.StartDate).IsRequired();
        builder.Property(o => o.EndDate).IsRequired();

        builder.Property(o => o.Status)
               .IsRequired()
               .HasMaxLength(50);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AgentDateOverrides_Status",
            "[Status] IN ('Available', 'Unavailable')"));

        builder.Property(o => o.DateCreation).IsRequired();

        builder.HasOne(o => o.Agent)
               .WithMany(a => a.DateOverrides)
               .HasForeignKey(o => o.AgentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
