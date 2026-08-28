using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <summary>
    /// Data repair, not a schema change. Two independent problems found by a
    /// data-consistency audit of Beaulieu Zenata Ecocité:
    ///
    /// 1) Projects.StatusGlobal held a mix of legacy commercial/construction
    ///    spellings ("Available", "UnderConstruction") alongside canonical §3
    ///    lifecycle codes. The frontend already collapses both legacy values
    ///    to IN_PROGRESS on read (api/http/enums.ts projectStatusString) —
    ///    this migration just writes that same, already-agreed mapping into
    ///    the data so the ambiguity doesn't have to be re-resolved on every
    ///    render. "ComingSoon"/"Sold" do not currently occur in this table;
    ///    included for completeness in case they appear later.
    ///
    /// 2) Two projects have OverAllProgress = 0 despite confirmed unit sales
    ///    (Beaulieu Zenata Ecocité: 162/312 sold; Jardin d'Atlantis: 2/2
    ///    sold) — an Excel-import gap, not a real 0% construction state.
    ///    No construction-progress source exists elsewhere in the data, so
    ///    sell-through percentage is used as the best available proxy.
    ///
    /// Also: Beaulieu Zenata Ecocité's Location held raw GPS coordinates
    /// ("33.6518004,-7.4706751") instead of a place name. Its Address column
    /// (a Google Maps link) confirms the same coordinates as "Beaulieu
    /// Zenata Eco-Cité", and its sibling project "Beaulieu Residences" is
    /// already stored as "Zenata, Mohammedia" — reused here for consistency.
    /// </summary>
    public partial class FixProjectStatusVocabularyAndZenataData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Projects] SET [StatusGlobal] = N'IN_PROGRESS' WHERE [StatusGlobal] = N'Available';
                UPDATE [Projects] SET [StatusGlobal] = N'IN_PROGRESS' WHERE [StatusGlobal] = N'UnderConstruction';
                UPDATE [Projects] SET [StatusGlobal] = N'IN_PROGRESS' WHERE [StatusGlobal] = N'Sold';
                UPDATE [Projects] SET [StatusGlobal] = N'PLANNED' WHERE [StatusGlobal] = N'ComingSoon';

                UPDATE [Projects] SET [OverAllProgress] = 51.92 WHERE [Id] = '23C01558-88AF-4CA9-82C6-38D41F12C461';
                UPDATE [Projects] SET [OverAllProgress] = 100.00 WHERE [Id] = 'FF8B8444-41A7-41C1-84EE-8E96EB266D80';

                UPDATE [Projects] SET [Location] = N'Zenata, Mohammedia' WHERE [Id] = '23C01558-88AF-4CA9-82C6-38D41F12C461';
            ");
        }

        /// <summary>
        /// Not reversed: the legacy spellings and the 0% progress/coordinate
        /// location were import defects, not intentional data — there is no
        /// reason to want them back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
