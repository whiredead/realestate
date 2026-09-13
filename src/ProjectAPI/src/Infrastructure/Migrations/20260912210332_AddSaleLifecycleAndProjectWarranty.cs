using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <summary>
    /// §5.7 / §6 / §8 — gives Sales a lifecycle (Draft → PendingNotary →
    /// Confirmed / Cancelled), a link back to the reservation it realises, the
    /// price snapshot taken at creation, and a frozen warranty length; adds the
    /// project-level warranty setting the sale copies from.
    ///
    /// The scaffolded column defaults are NOT the values existing rows should
    /// carry — every pre-existing sale row only ever came into being because a
    /// notary recorded PURCHASE_COMPLETED, so the back-fill below promotes them
    /// to Confirmed and reconstructs the rest from data already present.
    /// Anything that cannot be reconstructed unambiguously is LEFT NULL and
    /// reported, never guessed.
    /// </summary>
    public partial class AddSaleLifecycleAndProjectWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_UnitId",
                table: "Sales");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Sales",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Sales",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalPrice",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Sales",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAmount",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservationAmount",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyMonths",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyMonths",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 12);

            // ----------------------------------------------------------------
            // Back-fill (see the class summary for why each value is chosen).
            // ----------------------------------------------------------------

            // 1. Every existing sale is Confirmed. The only writer of this table
            //    before now was the notary conversion path, which creates a Sale
            //    at PURCHASE_COMPLETED — there is no such thing as a historic
            //    draft. Treating them as Draft (the column default) would make
            //    them editable and, worse, would be a lie about what happened.
            migrationBuilder.Sql("UPDATE [Sales] SET [Status] = 2;");

            // 2. CreatedAt / ConfirmedAt were not recorded separately; SaleDate
            //    is the one timestamp that exists, and for a sale born confirmed
            //    the two moments coincide.
            migrationBuilder.Sql(
                "UPDATE [Sales] SET [CreatedAt] = [SaleDate], [ConfirmedAt] = [SaleDate];");

            // 3. FinalPrice: no discount was ever recorded on a sale, so the
            //    agreed price is TotalPrice. ReservationAmount / RemainingAmount
            //    are deliberately LEFT NULL — the deposit lives on the
            //    reservation, and for rows whose reservation cannot be resolved
            //    below there is nothing to copy. NULL reads as "not recorded",
            //    0 would read as "nothing was paid".
            migrationBuilder.Sql("UPDATE [Sales] SET [FinalPrice] = [TotalPrice];");

            // 4. WarrantyMonths, frozen from the sale's project
            //    (Units.ProjectId -> Immeubles.Id, Immeubles.ProjectId ->
            //    Projects.Id). Projects.WarrantyMonths is 12 for every existing
            //    project, which is exactly what StartHandoverHandler used to
            //    hard-code, so no historic warranty changes length. A sale whose
            //    unit or building no longer resolves keeps 12 for the same
            //    reason rather than 0, which would mean "no warranty at all".
            migrationBuilder.Sql(@"
                UPDATE s
                SET s.[WarrantyMonths] = ISNULL(p.[WarrantyMonths], 12)
                FROM [Sales] s
                LEFT JOIN [Units] u ON u.[Id] = s.[UnitId]
                LEFT JOIN [Immeubles] i ON i.[Id] = u.[ProjectId]
                LEFT JOIN [Projects] p ON p.[Id] = i.[ProjectId];");

            // 5. ReservationId, resolved ONLY where it is unambiguous: exactly
            //    one CONVERTED (Status = 4) reservation on the same unit. Any
            //    unit carrying zero or several such reservations is left NULL —
            //    picking "the most recent" would fabricate a link between a sale
            //    and a file that may not have produced it, and that link is what
            //    the auto-confirmation path and the one-active-sale-per-
            //    reservation rule will key on from now on.
            migrationBuilder.Sql(@"
                UPDATE s
                SET s.[ReservationId] = r.[Id]
                FROM [Sales] s
                CROSS APPLY (
                    SELECT MIN(x.[Id]) AS [Id], COUNT(*) AS [Matches]
                    FROM [Reservations] x
                    WHERE x.[UnitId] = s.[UnitId] AND x.[Status] = 4
                ) r
                WHERE r.[Matches] = 1;");

            // 6. Report what could not be reconstructed. PRINT surfaces in the
            //    `dotnet ef database update` / DACPAC output; the rows keep a
            //    NULL ReservationId and stay perfectly usable — the filtered
            //    unique index below excludes NULLs, so they neither block a unit
            //    at the reservation level nor get silently paired off.
            migrationBuilder.Sql(@"
                DECLARE @unresolved int =
                    (SELECT COUNT(*) FROM [Sales] WHERE [ReservationId] IS NULL);
                IF @unresolved > 0
                    PRINT CONCAT(
                        'AddSaleLifecycleAndProjectWarranty: ', @unresolved,
                        ' sale(s) could not be matched to a single CONVERTED reservation ',
                        'and keep ReservationId = NULL. Query: SELECT Id, UnitId, SaleDate ',
                        'FROM Sales WHERE ReservationId IS NULL;');");

            // 7. Precondition for IX_Sales_ActivePerUnit. Every row is now
            //    Confirmed, i.e. active, so the index demands at most one sale
            //    row per unit across all history. If a unit was sold twice in
            //    the legacy data, creating the index would fail with a bare
            //    "duplicate key" and no indication of which unit. Fail here
            //    instead, naming them: which of the two rows is the real sale is
            //    a business decision, not something a migration may invent by
            //    cancelling one.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Sales] GROUP BY [UnitId] HAVING COUNT(*) > 1)
                BEGIN
                    DECLARE @dupes nvarchar(2000) =
                        (SELECT STRING_AGG(CONVERT(nvarchar(36), [UnitId]), ', ')
                         FROM (SELECT TOP (20) [UnitId]
                               FROM [Sales]
                               GROUP BY [UnitId]
                               HAVING COUNT(*) > 1) d);
                    DECLARE @msg nvarchar(4000) = CONCAT(
                        'Cannot enforce one active sale per unit: several Sales rows share a UnitId. ',
                        'Resolve them (cancel the superseded sale, i.e. set Status = 3) before applying ',
                        'this migration. Affected UnitId(s): ', @dupes);
                    RAISERROR(@msg, 16, 1);
                END;");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ActivePerReservation",
                table: "Sales",
                column: "ReservationId",
                unique: true,
                filter: "[ReservationId] IS NOT NULL AND [Status] IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ActivePerUnit",
                table: "Sales",
                column: "UnitId",
                unique: true,
                filter: "[Status] IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ReservationId",
                table: "Sales",
                column: "ReservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_ActivePerReservation",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ActivePerUnit",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ReservationId",
                table: "Sales");

            // Down drops the columns, so any Draft / PendingNotary sale created
            // after this migration loses the fact that it was not yet final.
            // Reverting is only safe while no such row exists.
            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "FinalPrice",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "RemainingAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReservationAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "WarrantyMonths",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "WarrantyMonths",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_UnitId",
                table: "Sales",
                column: "UnitId");
        }
    }
}
