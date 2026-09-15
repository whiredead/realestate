using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitTypeBien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TypeBienId",
                table: "Units",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_TypeBienId",
                table: "Units",
                column: "TypeBienId");

            migrationBuilder.AddForeignKey(
                name: "FK_Units_TypeBiens_TypeBienId",
                table: "Units",
                column: "TypeBienId",
                principalTable: "TypeBiens",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Units_TypeBiens_TypeBienId",
                table: "Units");

            migrationBuilder.DropIndex(
                name: "IX_Units_TypeBienId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "TypeBienId",
                table: "Units");
        }
    }
}
