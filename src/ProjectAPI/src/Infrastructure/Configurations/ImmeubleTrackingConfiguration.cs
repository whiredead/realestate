using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    public class ImmeubleTrackingConfiguration : IEntityTypeConfiguration<ImmeubleTracking>
    {
        public void Configure(EntityTypeBuilder<ImmeubleTracking> builder)
        {
            builder.ToTable("ImmeubleTracking");
            builder.HasKey(it => it.Id);

            builder.Property(it => it.StatusUpdate)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(it => it.DateUpdated)
                .IsRequired();

            builder.HasOne(it => it.Immeuble)
                .WithMany()
                .HasForeignKey(it => it.ImmeubleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
