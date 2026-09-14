using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalVisitReportFeedbackFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientFeedback",
                table: "FinalVisitReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectiveAction",
                table: "FinalVisitReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpNotes",
                table: "FinalVisitReports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NonComplianceReason",
                table: "FinalVisitReports",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientFeedback",
                table: "FinalVisitReports");

            migrationBuilder.DropColumn(
                name: "CorrectiveAction",
                table: "FinalVisitReports");

            migrationBuilder.DropColumn(
                name: "FollowUpNotes",
                table: "FinalVisitReports");

            migrationBuilder.DropColumn(
                name: "NonComplianceReason",
                table: "FinalVisitReports");
        }
    }
}
