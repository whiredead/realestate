using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.FeedBacks.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

/// <summary>
/// Configuration for the Feedback entity.
/// </summary>
public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    /// <summary>
    /// Configures the properties and relationships of the Feedback entity.
    /// </summary>
    /// <param name="builder">The entity type builder for Feedback.</param>
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("Feedbacks");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.UserId).IsRequired();
        builder.Property(f => f.Rating).IsRequired();
        builder.Property(f => f.Comments).HasMaxLength(1000);
        builder.Property(f => f.CreatedAt).IsRequired();
        
        // Configure Attachments as comma-separated string
        builder.Property(f => f.Attachments).HasConversion(
            v => string.Join(',', v),
            v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

        // Configures the relationship between Feedback and User.
        builder.HasOne(f => f.User)
            .WithMany() // Assuming a User can have multiple Feedback entries
            .HasForeignKey(f => f.UserId);

        // Configures the relationship between Feedback and Project.
        builder.HasOne(f => f.FeedBack_Project)
            .WithMany() // Assuming a Project can have multiple Feedback entries
            .HasForeignKey(f => f.ProjectId);
    }
}