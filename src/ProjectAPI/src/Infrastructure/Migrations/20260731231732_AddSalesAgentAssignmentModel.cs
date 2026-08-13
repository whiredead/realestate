using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesAgentAssignmentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_AspNetUsers_AgentId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "UX_Appointments_ActiveSlotPerAgent",
                table: "Appointments");

            migrationBuilder.RenameColumn(
                name: "AgentId",
                table: "Appointments",
                newName: "SalesAgentId");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedByUserId",
                table: "Appointments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentSource",
                table: "Appointments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSalesAgentId",
                table: "Appointments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReassignmentReason",
                table: "Appointments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppointmentAssignmentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PreviousSalesAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignmentSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentAssignmentHistories", x => x.Id);
                    table.CheckConstraint("CK_AppointmentAssignmentHistories_AssignmentSource", "[AssignmentSource] IN ('EXISTING_OWNER', 'ROUND_ROBIN', 'LOWEST_WORKLOAD', 'PRIMARY_AGENT', 'MANUAL_REASSIGNMENT')");
                    table.ForeignKey(
                        name: "FK_AppointmentAssignmentHistories_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentAssignmentConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrimaryAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LastAssignedAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LastAssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentAssignmentConfigs", x => x.Id);
                    table.CheckConstraint("CK_ProjectAgentAssignmentConfigs_RuleType", "[RuleType] IN ('ROUND_ROBIN', 'LOWEST_WORKLOAD', 'PRIMARY_AGENT')");
                    table.ForeignKey(
                        name: "FK_ProjectAgentAssignmentConfigs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_ActiveSlotPerAgent",
                table: "Appointments",
                columns: new[] { "SalesAgentId", "AppointmentDate" },
                unique: true,
                filter: "[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed') AND [SalesAgentId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_AssignmentSource",
                table: "Appointments",
                sql: "[AssignmentSource] IS NULL OR [AssignmentSource] IN ('EXISTING_OWNER', 'ROUND_ROBIN', 'LOWEST_WORKLOAD', 'PRIMARY_AGENT', 'MANUAL_REASSIGNMENT')");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentAssignmentHistories_AppointmentId",
                table: "AppointmentAssignmentHistories",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentAssignmentConfigs_ProjectId",
                table: "ProjectAgentAssignmentConfigs",
                column: "ProjectId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_AspNetUsers_SalesAgentId",
                table: "Appointments",
                column: "SalesAgentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_AspNetUsers_SalesAgentId",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "AppointmentAssignmentHistories");

            migrationBuilder.DropTable(
                name: "ProjectAgentAssignmentConfigs");

            migrationBuilder.DropIndex(
                name: "UX_Appointments_ActiveSlotPerAgent",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_AssignmentSource",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AssignedByUserId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AssignmentSource",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PreviousSalesAgentId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ReassignmentReason",
                table: "Appointments");

            migrationBuilder.RenameColumn(
                name: "SalesAgentId",
                table: "Appointments",
                newName: "AgentId");

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_ActiveSlotPerAgent",
                table: "Appointments",
                columns: new[] { "AgentId", "AppointmentDate" },
                unique: true,
                filter: "[Status] IN ('Requested', 'Confirmed', 'RescheduleProposed') AND [AgentId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_AspNetUsers_AgentId",
                table: "Appointments",
                column: "AgentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
