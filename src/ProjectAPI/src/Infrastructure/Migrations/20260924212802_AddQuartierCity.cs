using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Adds Quartiers.City (ville). The scaffold also proposed re-tightening ImportBatches.ProjectName to
    /// NOT NULL, undoing 20260922200500_ImportBatchProjectNullable because the model snapshot was out of
    /// step with that migration; that operation is intentionally left out here.
    /// </remarks>
    public partial class AddQuartierCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Quartiers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Quartiers");
        }
    }
}
