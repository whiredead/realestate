using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentCrmAndSlotIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_AgentId",
                table: "Appointments");

            // Normalise every legacy free-text status (e.g. the old
            // "En cours de traitement") to REQUESTED before the column shrinks
            // and the CHECK constraint is added below — otherwise both would
            // reject/truncate existing rows instead of the app's own
            // Enum.TryParse fallback handling them gracefully.
            migrationBuilder.Sql(
                "UPDATE [Appointments] SET [Status] = 'Requested' " +
                "WHERE [Status] NOT IN ('Requested', 'Confirmed', 'RescheduleProposed', 'Rejected', 'Cancelled', 'Completed', 'NoShow');");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Appointments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "CrmContactId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousAppointmentId",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CrmContactId",
                table: "Appointments",
                column: "CrmContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PreviousAppointmentId",
                table: "Appointments",
                column: "PreviousAppointmentId");

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_ActiveSlotPerAgent",
                table: "Appointments",
                columns: new[] { "AgentId", "AppointmentDate" },
                unique: true,
                filter: "[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed') AND [AgentId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_Status",
                table: "Appointments",
                sql: "[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed', 'Rejected', 'Cancelled', 'Completed', 'NoShow')");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Appointments_PreviousAppointmentId",
                table: "Appointments",
                column: "PreviousAppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_CrmContacts_CrmContactId",
                table: "Appointments",
                column: "CrmContactId",
                principalTable: "CrmContacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Appointments_PreviousAppointmentId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_CrmContacts_CrmContactId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CrmContactId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PreviousAppointmentId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "UX_Appointments_ActiveSlotPerAgent",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_Status",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CrmContactId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PreviousAppointmentId",
                table: "Appointments");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Appointments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AgentId",
                table: "Appointments",
                column: "AgentId");
        }
    }
}
