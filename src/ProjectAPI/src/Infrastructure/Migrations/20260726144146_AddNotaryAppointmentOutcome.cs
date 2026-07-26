using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotaryAppointmentOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "NotaryAppointments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeNote",
                table: "NotaryAppointments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutcomeRecordedAt",
                table: "NotaryAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeRecordedBy",
                table: "NotaryAppointments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotaryAppointments_Outcome",
                table: "NotaryAppointments",
                sql: "[Outcome] IS NULL OR [Outcome] IN ('PURCHASE_COMPLETED', 'INCOMPLETE_FILE', 'BUYER_ABSENT', 'POSTPONED', 'NOT_COMPLETED_OTHER')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NotaryAppointments_Outcome",
                table: "NotaryAppointments");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "NotaryAppointments");

            migrationBuilder.DropColumn(
                name: "OutcomeNote",
                table: "NotaryAppointments");

            migrationBuilder.DropColumn(
                name: "OutcomeRecordedAt",
                table: "NotaryAppointments");

            migrationBuilder.DropColumn(
                name: "OutcomeRecordedBy",
                table: "NotaryAppointments");
        }
    }
}
