using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// A building (immeuble) has two statuses only: EN_COURS_DE_CONSTRUCTION or CONSTRUIT. It used to
    /// carry the project phases (SUR_PLAN / EN_LIVRAISON / FINALISE), which contradicted the project
    /// it belongs to. Data only, no schema change: en livraison and finalisé buildings are built,
    /// everything else is still under construction.
    /// </remarks>
    public partial class BuildingStatusTwoValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE Immeubles
SET Status = CASE
    WHEN UPPER(LTRIM(RTRIM(ISNULL(Status, '')))) IN ('EN_LIVRAISON', 'FINALISE', 'FINALISÉ', 'FINALISED', 'FINALIZED', 'COMPLETED', 'DELIVERED', 'ARCHIVED', 'CONSTRUIT')
        THEN 'CONSTRUIT'
    ELSE 'EN_COURS_DE_CONSTRUCTION'
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best effort: the original three-way split cannot be recovered, only the two halves.
            migrationBuilder.Sql(@"
UPDATE Immeubles
SET Status = CASE Status
    WHEN 'CONSTRUIT' THEN 'EN_LIVRAISON'
    ELSE 'SUR_PLAN'
END;");
        }
    }
}
