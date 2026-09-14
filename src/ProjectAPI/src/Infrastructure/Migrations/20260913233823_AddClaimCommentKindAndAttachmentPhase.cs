using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimCommentKindAndAttachmentPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ClaimComments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NOTE");

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "ClaimAttachments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ClaimComments");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "ClaimAttachments");
        }
    }
}
