using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectStatusThreeValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Spec §7.5: a project has exactly three statuses. Existing rows carried
            // six internal codes plus legacy spellings; convert them in place.
            migrationBuilder.Sql(@"
UPDATE Projects SET StatusGlobal = CASE UPPER(LTRIM(RTRIM(ISNULL(StatusGlobal, ''))))
    WHEN 'EN_LIVRAISON' THEN 'EN_LIVRAISON'
    WHEN 'COMPLETED' THEN 'EN_LIVRAISON'
    WHEN 'DELIVERED' THEN 'EN_LIVRAISON'
    WHEN 'FINALISE' THEN 'FINALISE'
    WHEN 'ARCHIVED' THEN 'FINALISE'
    ELSE 'SUR_PLAN'
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the internal codes the previous version used.
            migrationBuilder.Sql(@"
UPDATE Projects SET StatusGlobal = CASE StatusGlobal
    WHEN 'EN_LIVRAISON' THEN 'COMPLETED'
    WHEN 'FINALISE' THEN 'ARCHIVED'
    ELSE 'IN_PROGRESS'
END;");
        }
    }
}
