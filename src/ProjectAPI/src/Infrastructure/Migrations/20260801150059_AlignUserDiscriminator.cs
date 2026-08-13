using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <summary>
    /// One-time data fix — see the HasDiscriminator comment in
    /// ApplicationDbContext.OnModelCreating. TPH only ever registered "User",
    /// "Agent" and "Notaire" as valid discriminator values; rows still holding
    /// the legacy "Acheteur" label (written before that mapping existed) fail
    /// materialization with "No discriminators matched the discriminator value
    /// 'Acheteur'" the moment anything loads them as a User (e.g.
    /// UserManager.FindByIdAsync in CreateAppointmentHandler). A buyer is a
    /// plain User — §6.2 makes the BUYER role, not an inheritance branch, what
    /// marks someone a buyer — so realigning to "User" changes no behavior,
    /// it only makes these rows loadable again.
    /// </summary>
    public partial class AlignUserDiscriminator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [AspNetUsers]
                SET [Discriminator] = N'User'
                WHERE [Discriminator] = N'Acheteur';
            ");
        }

        /// <summary>
        /// Not reversed: once realigned to "User", these rows are indistinguishable
        /// from every account that was already "User", so which ones were
        /// originally "Acheteur" can no longer be recovered.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
