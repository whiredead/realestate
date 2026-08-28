using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <summary>
    /// Same defect as 20260824145750_FixProjectStatusVocabularyAndZenataData,
    /// found one table over: Immeubles.Status held the identical legacy
    /// commercial/construction spellings alongside canonical §3 codes. All 10
    /// Beaulieu Zenata Ecocité buildings were "Available" pre-migration —
    /// missed by the first pass because that one only queried Projects.
    /// Same mapping the frontend already applies on read
    /// (api/http/enums.ts projectStatusString, reused for Immeuble.status in
    /// immeublesApi.ts): both legacy values collapse to IN_PROGRESS.
    /// </summary>
    public partial class FixImmeubleStatusVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Immeubles] SET [Status] = N'IN_PROGRESS' WHERE [Status] = N'Available';
                UPDATE [Immeubles] SET [Status] = N'IN_PROGRESS' WHERE [Status] = N'UnderConstruction';
                UPDATE [Immeubles] SET [Status] = N'IN_PROGRESS' WHERE [Status] = N'Sold';
                UPDATE [Immeubles] SET [Status] = N'PLANNED' WHERE [Status] = N'ComingSoon';
            ");
        }

        /// <summary>
        /// Not reversed: the legacy spellings were an import defect, not
        /// intentional data — there is no reason to want them back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
