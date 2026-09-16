using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations;

public partial class AddProjectStatusReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectStatusReferences",
            columns: table => new
            {
                Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ProjectStatusReferences", x => x.Code));

        migrationBuilder.InsertData(
            table: "ProjectStatusReferences",
            columns: new[] { "Code", "Label", "SortOrder", "IsActive" },
            values: new object[,]
            {
                { "SUR_PLAN", "Sur plan", 1, true },
                { "EN_LIVRAISON", "En livraison", 2, true },
                { "FINALISE", "Finalisé", 3, true }
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ProjectStatusReferences");
}
