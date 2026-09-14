using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class ProjectDocumentRequirementConfiguration : IEntityTypeConfiguration<ProjectDocumentRequirement>
{
    public void Configure(EntityTypeBuilder<ProjectDocumentRequirement> b)
    {
        b.ToTable("ProjectDocumentRequirements");
        b.HasKey(x => x.Id);

        b.Property(x => x.DocumentType).IsRequired().HasMaxLength(100);
        b.Property(x => x.LabelFr).IsRequired().HasMaxLength(200);
        b.Property(x => x.IsRequired).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(450);

        // One rule per document type per project. Two rows for "CIN" would let
        // a project both require and not require the same document, and the
        // checklist would show it twice.
        b.HasIndex(x => new { x.ProjectId, x.DocumentType })
            .IsUnique()
            .HasDatabaseName("IX_ProjectDocumentRequirements_ProjectType");

        // Deleting a project takes its requirements with it — they describe the
        // project's own submission rules and mean nothing without it. This is
        // configuration, not the reservation documents themselves (§9).
        b.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
