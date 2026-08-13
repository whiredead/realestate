using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotaryAppointmentSlotUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotaryAppointments_NotaireId",
                table: "NotaryAppointments");

            migrationBuilder.CreateIndex(
                name: "UX_NotaryAppointments_ActiveSlotPerNotary",
                table: "NotaryAppointments",
                columns: new[] { "NotaireId", "AppointmentDate" },
                unique: true,
                filter: "[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_NotaryAppointments_ActiveSlotPerNotary",
                table: "NotaryAppointments");

            migrationBuilder.CreateIndex(
                name: "IX_NotaryAppointments_NotaireId",
                table: "NotaryAppointments",
                column: "NotaireId");
        }
    }
}
