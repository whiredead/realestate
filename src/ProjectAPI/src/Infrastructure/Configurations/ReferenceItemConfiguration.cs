using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Projects.Entities;
namespace ProjectAPI.Infrastructure.Configurations;
public sealed class ReferenceItemConfiguration : IEntityTypeConfiguration<ReferenceItem>
{
 public void Configure(EntityTypeBuilder<ReferenceItem> b) { b.ToTable("ReferenceItems"); b.HasKey(x => x.Id); b.Property(x => x.Category).HasMaxLength(80).IsRequired(); b.Property(x => x.Code).HasMaxLength(80).IsRequired(); b.Property(x => x.Label).HasMaxLength(160).IsRequired(); b.Property(x => x.Description).HasMaxLength(2000); b.HasIndex(x => new { x.Category, x.Code }).IsUnique(); b.HasIndex(x => new { x.Category, x.SortOrder }); }
}
