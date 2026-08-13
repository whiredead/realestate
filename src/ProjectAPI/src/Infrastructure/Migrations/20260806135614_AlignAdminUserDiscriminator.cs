using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <summary>
    /// Same class of bug as AlignUserDiscriminator (20260801150059), and the
    /// same fix: TPH only ever registers "User", "Agent" and "Notaire" as
    /// valid discriminator values (ApplicationDbContext.OnModelCreating), but
    /// 2 real accounts — including the platform's own GLOBAL_ADMIN
    /// (yassine.aitmoussaa@gmail.com) — are stamped "Admin", a value nothing
    /// maps to. Any query materializing one of these rows as a User throws
    /// "No discriminators matched the discriminator value 'Admin'" (found via
    /// GetLeadsHandler's Users lookup, but the landmine predates it — any
    /// broad Users query would trip it). §6.2 makes the GLOBAL_ADMIN/PROJECT_ADMIN
    /// role, not an inheritance branch, what marks someone an admin, so
    /// realigning to "User" changes no behavior — it only makes these rows
    /// loadable again.
    /// </summary>
    public partial class AlignAdminUserDiscriminator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [AspNetUsers]
                SET [Discriminator] = N'User'
                WHERE [Discriminator] = N'Admin';
            ");
        }

        /// <summary>
        /// Not reversed: once realigned to "User", these rows are indistinguishable
        /// from every account that was already "User", so which ones were
        /// originally "Admin" can no longer be recovered.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
