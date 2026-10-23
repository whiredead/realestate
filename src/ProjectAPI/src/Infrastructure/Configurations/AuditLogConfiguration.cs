using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Audit.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Category).IsRequired().HasMaxLength(20);
        b.Property(x => x.Action).IsRequired().HasMaxLength(100);
        b.Property(x => x.Outcome).IsRequired().HasMaxLength(20);
        b.Property(x => x.ActorUserId).HasMaxLength(450);
        b.Property(x => x.ActorLabel).HasMaxLength(256);
        b.Property(x => x.ActorRoles).HasMaxLength(256);
        b.Property(x => x.EntityType).HasMaxLength(100);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Detail).HasMaxLength(500);
        b.Property(x => x.RequestId).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(64);

        // The three ways the screens read the log: per project, per user, by date.
        b.HasIndex(x => new { x.ProjectId, x.OccurredAt });
        b.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
        b.HasIndex(x => new { x.Category, x.OccurredAt });

        b.HasMany(x => x.Subjects).WithOne().HasForeignKey(s => s.AuditLogId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogSubjectConfiguration : IEntityTypeConfiguration<AuditLogSubject>
{
    public void Configure(EntityTypeBuilder<AuditLogSubject> b)
    {
        b.ToTable("AuditLogSubjects");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
        b.Property(x => x.EntityId).IsRequired().HasMaxLength(64);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
