using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration for the Lead entity.
    /// </summary>
    public class LeadConfiguration : IEntityTypeConfiguration<Lead>
    {
        public void Configure(EntityTypeBuilder<Lead> builder)
        {
            // Configure table name
            builder.ToTable("Leads");

            // Configure primary key
            builder.HasKey(l => l.Id);

            // Configure properties
            builder.Property(l => l.Id)
                .IsRequired();

            builder.Property(l => l.UserId)
                .HasMaxLength(450) // Assuming string for UserId (e.g., AspNetUsers primary key)
                .IsRequired(false);

            builder.Property(l => l.ProjectId)
                .IsRequired();

            builder.Property(l => l.AgentId)
                .HasMaxLength(450)
                .IsRequired(false);

            builder.Property(l => l.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETDATE()");

            builder.Property(l => l.Description)
                .HasMaxLength(1000)
                .IsRequired(false);

            builder.HasOne(l => l.Project)
                .WithMany()
                .HasForeignKey(l => l.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
