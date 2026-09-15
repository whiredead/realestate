using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImmeubleStatusThreeValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Buildings now use the project's three statuses; existing ones take
            // their project's current status.
            migrationBuilder.Sql(@"
UPDATE i SET i.Status = CASE
    WHEN UPPER(ISNULL(p.StatusGlobal, '')) IN ('EN_LIVRAISON', 'COMPLETED', 'DELIVERED') THEN 'EN_LIVRAISON'
    WHEN UPPER(ISNULL(p.StatusGlobal, '')) IN ('FINALISE', 'ARCHIVED') THEN 'FINALISE'
    ELSE 'SUR_PLAN' END
FROM Immeubles i INNER JOIN Projects p ON p.Id = i.ProjectId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE Immeubles SET Status = CASE Status
    WHEN 'EN_LIVRAISON' THEN 'COMPLETED'
    WHEN 'FINALISE' THEN 'ARCHIVED'
    ELSE 'ComingSoon' END;");
        }
    }
}
