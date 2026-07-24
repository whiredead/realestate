using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Infrastructure.Configurations
{
    public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.ToTable("Reservations");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name).HasMaxLength(100).IsRequired(false);
            builder.Property(r => r.LastName).HasMaxLength(100).IsRequired(false);
            builder.Property(r => r.CIN).HasMaxLength(50).IsRequired(false);
            builder.Property(r => r.Email).HasMaxLength(150).IsRequired(false);
            builder.Property(r => r.PhoneNumber).HasMaxLength(20).IsRequired(false);

            builder.Property(r => r.TotalPropertyPrice).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(r => r.ReservationAmount).HasColumnType("decimal(18,2)").IsRequired();

            builder.Property(r => r.Status)
             .HasConversion<int>() // store enum as int
             .IsRequired();

            builder.Property(r => r.AdminNote).HasMaxLength(2000);

            builder.HasOne(r => r.Unit)
                .WithMany()
                .HasForeignKey(r => r.UnitId)
                .OnDelete(DeleteBehavior.Cascade);
           


            builder.HasMany(r => r.Documents)
                   .WithOne(d => d.Reservation)
                   .HasForeignKey(d => d.ReservationId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
