using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public sealed class ProjectStatusReferenceConfiguration : IEntityTypeConfiguration<ProjectStatusReference>
{
    public void Configure(EntityTypeBuilder<ProjectStatusReference> builder)
    {
        builder.ToTable("ProjectStatusReferences");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BusinessPhase).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
    }
}
