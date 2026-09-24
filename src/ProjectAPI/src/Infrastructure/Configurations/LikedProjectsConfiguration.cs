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

        // No relationship to User on purpose: UserId holds either a real
        // AspNetUsers.Id or an anonymous guest id (signed-out visitors can
        // favourite too), and a guest id never has a matching Users row —
        // a required FK here would reject every anonymous like.
        builder.Property(lp => lp.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(lp => lp.UserId);
    }
}