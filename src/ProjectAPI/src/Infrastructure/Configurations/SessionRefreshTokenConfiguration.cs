using Microsoft.EntityFrameworkCore; using Microsoft.EntityFrameworkCore.Metadata.Builders; using ProjectAPI.Domain.Identity.Entities;
namespace ProjectAPI.Infrastructure.Configurations;
public sealed class SessionRefreshTokenConfiguration : IEntityTypeConfiguration<SessionRefreshToken> { public void Configure(EntityTypeBuilder<SessionRefreshToken> b) { b.ToTable("SessionRefreshTokens"); b.HasKey(x=>x.Id); b.Property(x=>x.UserId).HasMaxLength(450).IsRequired(); b.Property(x=>x.TokenHash).HasMaxLength(64).IsRequired(); b.HasIndex(x=>x.TokenHash).IsUnique(); } }
