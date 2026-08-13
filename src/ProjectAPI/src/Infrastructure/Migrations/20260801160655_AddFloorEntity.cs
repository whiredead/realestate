using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FloorId is added nullable first, and the old Floor string column
            // is kept until the backfill below has read it — dropping it up
            // front (as originally scaffolded) would discard every existing
            // unit's floor label with no way to recover it.
            migrationBuilder.AddColumn<Guid>(
                name: "FloorId",
                table: "Units",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Floors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImmeubleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Floors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Floors_Immeubles_ImmeubleId",
                        column: x => x.ImmeubleId,
                        principalTable: "Immeubles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Units_FloorId",
                table: "Units",
                column: "FloorId");

            migrationBuilder.CreateIndex(
                name: "UX_Floors_ImmeubleName",
                table: "Floors",
                columns: new[] { "ImmeubleId", "Name" },
                unique: true);

            // Backfill — one Floor row per distinct (building, floor label)
            // pair actually present in the data today, blank labels folded to
            // "Non spécifié" so every unit still resolves to a real Floor row.
            migrationBuilder.Sql(@"
                INSERT INTO [Floors] ([Id], [ImmeubleId], [Name], [SequenceNo], [CreatedAt])
                SELECT NEWID(), src.[ProjectId], src.[FloorName],
                       ROW_NUMBER() OVER (PARTITION BY src.[ProjectId] ORDER BY src.[FloorName]) - 1,
                       SYSUTCDATETIME()
                FROM (
                    SELECT DISTINCT u.[ProjectId],
                           NULLIF(LTRIM(RTRIM(u.[Floor])), '') AS FloorNameRaw,
                           COALESCE(NULLIF(LTRIM(RTRIM(u.[Floor])), ''), N'Non spécifié') AS FloorName
                    FROM [Units] u
                ) AS src;

                UPDATE u
                SET u.[FloorId] = f.[Id]
                FROM [Units] u
                INNER JOIN [Floors] f
                    ON f.[ImmeubleId] = u.[ProjectId]
                   AND f.[Name] = COALESCE(NULLIF(LTRIM(RTRIM(u.[Floor])), ''), N'Non spécifié');
            ");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "Units");

            migrationBuilder.AlterColumn<Guid>(
                name: "FloorId",
                table: "Units",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Units_Floors_FloorId",
                table: "Units",
                column: "FloorId",
                principalTable: "Floors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Restores the Floor string column, but empty for every row: once
        /// distinct labels are consolidated into shared Floor rows per
        /// building, the original per-unit text isn't recoverable from that.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Units_Floors_FloorId",
                table: "Units");

            migrationBuilder.DropTable(
                name: "Floors");

            migrationBuilder.DropIndex(
                name: "IX_Units_FloorId",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "FloorId",
                table: "Units");

            migrationBuilder.AddColumn<string>(
                name: "Floor",
                table: "Units",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
