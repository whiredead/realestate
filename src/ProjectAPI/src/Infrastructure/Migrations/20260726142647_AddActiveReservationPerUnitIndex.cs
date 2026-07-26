using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveReservationPerUnitIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ActivePerUnit",
                table: "Reservations",
                column: "UnitId",
                unique: true,
                filter: "[Status] IN (0, 1, 4, 6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_ActivePerUnit",
                table: "Reservations");
        }
    }
}
