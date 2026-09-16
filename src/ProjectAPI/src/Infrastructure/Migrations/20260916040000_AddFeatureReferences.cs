using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ProjectAPI.Infrastructure.Migrations;

public partial class AddFeatureReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "FeatureReferences", columns: table => new
        {
            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
            Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
            IconUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
            Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
            IsActive = table.Column<bool>(type: "bit", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_FeatureReferences", x => x.Id));
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "FeatureReferences");
}
