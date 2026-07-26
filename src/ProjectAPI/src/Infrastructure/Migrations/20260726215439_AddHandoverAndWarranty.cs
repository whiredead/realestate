using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoverAndWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HandoverAppointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SalesAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PreviousAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverAppointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warranties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarrantyTypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warranties", x => x.Id);
                    table.CheckConstraint("CK_Warranties_Period", "[EndsAt] > [StartsAt]");
                });

            migrationBuilder.CreateTable(
                name: "HandoverReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Participants = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Observations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedByContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoverReports_HandoverAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "HandoverAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HandoverItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverItems", x => x.Id);
                    table.CheckConstraint("CK_HandoverItems_Quantity", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_HandoverItems_HandoverReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "HandoverReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverAppointments_ReservationId",
                table: "HandoverAppointments",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverAppointments_UnitId",
                table: "HandoverAppointments",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "UX_HandoverAppointments_ActivePerReservation",
                table: "HandoverAppointments",
                column: "ReservationId",
                unique: true,
                filter: "[Status] IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverItems_ReportId",
                table: "HandoverItems",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "UX_HandoverReports_AppointmentVersion",
                table: "HandoverReports",
                columns: new[] { "AppointmentId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warranties_UnitId",
                table: "Warranties",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "UX_Warranties_ActivePerUnitType",
                table: "Warranties",
                columns: new[] { "UnitId", "WarrantyTypeCode" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandoverItems");

            migrationBuilder.DropTable(
                name: "Warranties");

            migrationBuilder.DropTable(
                name: "HandoverReports");

            migrationBuilder.DropTable(
                name: "HandoverAppointments");
        }
    }
}
