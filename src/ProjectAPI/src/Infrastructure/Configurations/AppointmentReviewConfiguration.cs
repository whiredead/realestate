using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Appointments.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

/// <summary>
/// Configuration class for the <see cref="AppointmentReview"/> entity.
/// </summary>
public class AppointmentReviewConfiguration : IEntityTypeConfiguration<AppointmentReview>
{
    /// <summary>
    /// Configures the properties and relationships of the <see cref="AppointmentReview"/> entity.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity.</param>
    public void Configure(EntityTypeBuilder<AppointmentReview> builder)
    {
        // Configures the table name for AppointmentReview.
        builder.ToTable("AppointmentReviews");

        // Defines the primary key for AppointmentReview.
        builder.HasKey(ar => ar.Id);

        // Configures the AppointmentId property to be required.
        builder.Property(ar => ar.AppointmentId).IsRequired();

        // Configures the rating properties to be required.
        builder.Property(ar => ar.OverallRating).IsRequired();
        builder.Property(ar => ar.ProfessionalismRating).IsRequired();
        builder.Property(ar => ar.CommunicationRating).IsRequired();
        builder.Property(ar => ar.PunctualityRating).IsRequired();
        builder.Property(ar => ar.PropertyKnowledgeRating).IsRequired();

        // Configures the Comments property with a maximum length of 1000 characters.
        builder.Property(ar => ar.Comments).HasMaxLength(1000);

        // Configures the CreatedAt property to be required.
        builder.Property(ar => ar.CreatedAt).IsRequired();

        // Configures the ClientName property with a maximum length of 150 characters.
        builder.Property(ar => ar.ClientName).HasMaxLength(150);

        // Configures the ClientEmail property with a maximum length of 150 characters.
        builder.Property(ar => ar.ClientEmail).HasMaxLength(150);

        // Configures the relationship between AppointmentReview and Appointment.
        builder.HasOne(ar => ar.Appointment)
            .WithMany(a => a.Reviews)
            .HasForeignKey(ar => ar.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}