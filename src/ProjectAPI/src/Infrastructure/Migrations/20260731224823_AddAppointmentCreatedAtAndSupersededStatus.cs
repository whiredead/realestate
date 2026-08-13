using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentCreatedAtAndSupersededStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_Status",
                table: "Appointments");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing rows have no real creation timestamp; AppointmentDate is
            // the best available approximation (the requested slot, not the
            // actual creation instant, but far closer than the column default).
            migrationBuilder.Sql("UPDATE [Appointments] SET [CreatedAt] = [AppointmentDate];");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_Status",
                table: "Appointments",
                sql: "[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed', 'Rejected', 'Cancelled', 'Completed', 'NoShow', 'Superseded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_Status",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Appointments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_Status",
                table: "Appointments",
                sql: "[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed', 'Rejected', 'Cancelled', 'Completed', 'NoShow')");
        }
    }
}
