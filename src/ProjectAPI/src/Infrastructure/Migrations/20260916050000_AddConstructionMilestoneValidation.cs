using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ProjectAPI.Infrastructure.Migrations;
public partial class AddConstructionMilestoneValidation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<bool>(name: "IsValidated", table: "ConstructionMilestones", type: "bit", nullable: false, defaultValue: false);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name: "IsValidated", table: "ConstructionMilestones");
}
