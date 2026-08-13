using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentVisitReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentVisitReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    PropertiesPresentedUnitIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InterestLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfirmedBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ConfirmedRequirements = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Objections = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NextAction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FollowUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VisitResult = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InternalNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentVisitReports", x => x.Id);
                    table.CheckConstraint("CK_AppointmentVisitReports_InterestLevel", "[InterestLevel] IN ('LOW', 'MEDIUM', 'HIGH', 'VERY_HIGH')");
                    table.CheckConstraint("CK_AppointmentVisitReports_VisitResult", "[VisitResult] IN ('INTERESTED_FOLLOW_UP', 'READY_TO_RESERVE', 'NOT_INTERESTED', 'NO_SHOW', 'RESCHEDULE_NEEDED')");
                    table.ForeignKey(
                        name: "FK_AppointmentVisitReports_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentVisitReports_AppointmentId",
                table: "AppointmentVisitReports",
                column: "AppointmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentVisitReports");
        }
    }
}
