using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    public partial class RemoveImmeublePriceRange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MinPrice", table: "Immeubles");
            migrationBuilder.DropColumn(name: "MaxPrice", table: "Immeubles");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "MinPrice", table: "Immeubles", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "MaxPrice", table: "Immeubles", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        }
    }
}
