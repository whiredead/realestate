using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Infrastructure.Configurations;

public class ReservationDocumentConfiguration : IEntityTypeConfiguration<ReservationDocument>
{
    public void Configure(EntityTypeBuilder<ReservationDocument> builder)
    {
        builder.ToTable("ReservationDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).HasMaxLength(300).IsRequired();
        builder.Property(d => d.Url).HasMaxLength(2000).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(150);
        builder.Property(d => d.DocumentType).HasMaxLength(100);
    }
}