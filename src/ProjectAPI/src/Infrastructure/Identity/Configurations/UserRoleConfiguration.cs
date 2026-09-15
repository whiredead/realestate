using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProjectAPI.Infrastructure.Identity.Configurations;

internal class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder
            .HasOne(u => u.Role)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(u => u.RoleId);

        builder
            .HasOne(u => u.User)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(u => u.UserId);
    }
}
