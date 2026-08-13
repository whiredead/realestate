using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Imports.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatches");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.FileName).IsRequired().HasMaxLength(260);
        builder.Property(b => b.FileHash).IsRequired().HasMaxLength(64);
        builder.Property(b => b.CreatedBy).HasMaxLength(450);
        builder.Property(b => b.FailureReason).HasMaxLength(2000);
        builder.Property(b => b.Status).HasConversion<int>().IsRequired();

        builder.HasIndex(b => new { b.ProjectId, b.Status });

        builder.HasMany(b => b.Rows)
            .WithOne(r => r.Batch)
            .HasForeignKey(r => r.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("ImportRows");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Sheet).IsRequired().HasMaxLength(50);
        builder.Property(r => r.RawData).IsRequired();
        builder.Property(r => r.Errors).HasMaxLength(2000);

        builder.HasIndex(r => new { r.BatchId, r.Sheet, r.RowNumber });
    }
}
