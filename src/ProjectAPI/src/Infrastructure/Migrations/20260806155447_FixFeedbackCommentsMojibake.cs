using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <summary>
    /// N28 — data repair, not a code fix: 4 of 6 Feedbacks.Comments rows were
    /// double-encoded (their accented characters' real UTF-8 bytes were each
    /// separately reinterpreted as Latin-1/Windows-1252 codepoints before
    /// being stored — "é" (UTF-8 0xC3 0xA9) became the two characters "Ã©").
    /// Confirmed byte-for-byte via CAST(Comments AS VARBINARY): the stored
    /// UTF-16 data itself contains the mojibake codepoints, not a display bug
    /// (same underlying mechanism as N4's catalogue mojibake, but baked into
    /// the data rather than a read-path issue — no application code writes
    /// or seeds this table's Comments from any source in this repo, so there
    /// is nothing to fix except the rows themselves).
    /// </summary>
    public partial class FixFeedbackCommentsMojibake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE [Feedbacks] SET [Comments] = N'Studio livré en avance, finitions impeccables.' WHERE [Id] = '9A309767-D6A2-4DC7-BD36-61EFE4C7DD5E';
                UPDATE [Feedbacks] SET [Comments] = N'Plateaux très bien conçus, équipe commerciale réactive.' WHERE [Id] = '8AE8805A-37F4-4268-A892-E6F04767310A';
                UPDATE [Feedbacks] SET [Comments] = N'Projet prometteur, communication chantier à améliorer.' WHERE [Id] = '82DD16BA-FF40-4C17-9234-788BC25C9F9E';
                UPDATE [Feedbacks] SET [Comments] = N'Bon rapport qualité-prix mais délais un peu longs.' WHERE [Id] = '97502110-F81C-44F3-BCDE-5C9E69C6002F';
            ");
        }

        /// <summary>
        /// Not reversed: the original mojibake bytes aren't recoverable from
        /// the corrected text (the double-encoding wasn't a lossless
        /// transform in the other direction for every codepoint), and there
        /// is no reason to want the corrupted text back.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
