using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    public partial class AddTypeBienPriceRange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "MinPrice", table: "TypeBiens", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "MaxPrice", table: "TypeBiens", type: "decimal(18,2)", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MinPrice", table: "TypeBiens");
            migrationBuilder.DropColumn(name: "MaxPrice", table: "TypeBiens");
        }
    }
}
