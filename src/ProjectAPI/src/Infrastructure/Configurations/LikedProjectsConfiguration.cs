using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class LikedProjectsConfiguration : IEntityTypeConfiguration<LikedProject>
{
    public void Configure(EntityTypeBuilder<LikedProject> builder)
    {
        // Configure Project entity

        builder.ToTable("LikedProjects");
        builder.HasKey(p => p.Id);

        builder.HasOne(lp=>lp.Project)
            .WithMany()
            .HasForeignKey(lp=>lp.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}