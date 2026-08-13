using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class ProjectAgentAssignmentConfigConfiguration : IEntityTypeConfiguration<ProjectAgentAssignmentConfig>
{
    public void Configure(EntityTypeBuilder<ProjectAgentAssignmentConfig> builder)
    {
        builder.ToTable("ProjectAgentAssignmentConfigs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RuleType).IsRequired().HasMaxLength(30);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ProjectAgentAssignmentConfigs_RuleType",
            "[RuleType] IN ('ROUND_ROBIN', 'LOWEST_WORKLOAD', 'PRIMARY_AGENT')"));

        builder.Property(x => x.PrimaryAgentId).HasMaxLength(450);
        builder.Property(x => x.LastAssignedAgentId).HasMaxLength(450);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);

        // One config per project.
        builder.HasIndex(x => x.ProjectId).IsUnique();

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
