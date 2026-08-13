using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotaryAppointmentAssignmentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "NotaryAppointments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing rows have no real creation timestamp; AppointmentDate is
            // the best available approximation, same reasoning as the
            // commercial Appointments migration.
            migrationBuilder.Sql("UPDATE [NotaryAppointments] SET [CreatedAt] = [AppointmentDate];");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousAppointmentId",
                table: "NotaryAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousNotaireId",
                table: "NotaryAppointments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReassignmentReason",
                table: "NotaryAppointments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotaryAppointmentAssignmentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaryAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaireId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PreviousNotaireId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignmentSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotaryAppointmentAssignmentHistories", x => x.Id);
                    table.CheckConstraint("CK_NotaryAppointmentAssignmentHistories_AssignmentSource", "[AssignmentSource] IN ('MANUAL', 'MANUAL_REASSIGNMENT')");
                    table.ForeignKey(
                        name: "FK_NotaryAppointmentAssignmentHistories_NotaryAppointments_NotaryAppointmentId",
                        column: x => x.NotaryAppointmentId,
                        principalTable: "NotaryAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotaryAppointments_PreviousAppointmentId",
                table: "NotaryAppointments",
                column: "PreviousAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotaryAppointmentAssignmentHistories_NotaryAppointmentId",
                table: "NotaryAppointmentAssignmentHistories",
                column: "NotaryAppointmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_NotaryAppointments_NotaryAppointments_PreviousAppointmentId",
                table: "NotaryAppointments",
                column: "PreviousAppointmentId",
                principalTable: "NotaryAppointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotaryAppointments_NotaryAppointments_PreviousAppointmentId",
                table: "NotaryAppointments");

            migrationBuilder.DropTable(
                name: "NotaryAppointmentAssignmentHistories");

            migrationBuilder.DropIndex(
                name: "IX_NotaryAppointments_PreviousAppointmentId",
                table: "NotaryAppointments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "NotaryAppointments");

            migrationBuilder.DropColumn(
                name: "PreviousAppointmentId",
                table: "NotaryAppointments");

            migrationBuilder.DropColumn(
                name: "PreviousNotaireId",
                table: "NotaryAppointments");

            migrationBuilder.DropColumn(
                name: "ReassignmentReason",
                table: "NotaryAppointments");
        }
    }
}
