using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ProjectAPI.Infrastructure.Context;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations;

/// <summary>
/// Adds a display-status reference to projects while preserving StatusGlobal as
/// the lifecycle phase used by reservation, delivery and read-only safeguards.
/// This is deliberately additive because the migration snapshot predates other
/// production migrations already present in the database.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260919150000_AddProjectStatusReferencePhase")]
public partial class AddProjectStatusReferencePhase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BusinessPhase",
            table: "ProjectStatusReferences",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "SUR_PLAN");

        migrationBuilder.AddColumn<string>(
            name: "StatusReferenceCode",
            table: "Projects",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE ProjectStatusReferences
            SET BusinessPhase = CASE Code
                WHEN 'EN_LIVRAISON' THEN 'EN_LIVRAISON'
                WHEN 'FINALISE' THEN 'FINALISE'
                ELSE 'SUR_PLAN'
            END;

            UPDATE p
            SET StatusReferenceCode = p.StatusGlobal
            FROM Projects p
            INNER JOIN ProjectStatusReferences r ON r.Code = p.StatusGlobal
            WHERE p.StatusReferenceCode IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BusinessPhase", table: "ProjectStatusReferences");
        migrationBuilder.DropColumn(name: "StatusReferenceCode", table: "Projects");
    }
}
