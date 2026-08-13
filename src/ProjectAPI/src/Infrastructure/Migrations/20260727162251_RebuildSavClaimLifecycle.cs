using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RebuildSavClaimLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old ClaimStatus enum (New=0, Read=1, InProgress=2, Resolved=3,
            // Rejected=4) does not share numeric values with the new 10-state one
            // (Submitted=0, UnderReview=1, MoreInfoRequired=2, Assigned=3,
            // InProgress=4, WaitingCustomer=5, Resolved=6, Rejected=7,
            // Cancelled=8, Closed=9). Column type is unchanged (int), so EF
            // scaffolded nothing here — without this remap, existing rows would
            // silently reinterpret under the new vocabulary (old InProgress=2
            // would read as the new MoreInfoRequired=2, old Resolved=3 as the
            // new Assigned=3). Remap in descending old-value order so no row is
            // rewritten twice inside the same statement.
            migrationBuilder.Sql(
                "UPDATE [AfterSaleClaims] SET [Status] = 7 WHERE [Status] = 4; " + // Rejected 4 -> 7
                "UPDATE [AfterSaleClaims] SET [Status] = 6 WHERE [Status] = 3; " + // Resolved 3 -> 6
                "UPDATE [AfterSaleClaims] SET [Status] = 4 WHERE [Status] = 2; " + // InProgress 2 -> 4
                "UPDATE [AfterSaleClaims] SET [Status] = 1 WHERE [Status] = 1; " + // Read 1 -> UnderReview 1 (no-op, same value, listed for completeness)
                "UPDATE [AfterSaleClaims] SET [Status] = 0 WHERE [Status] = 0;");  // New 0 -> Submitted 0 (no-op)

            // ClaimHistory.FromStatus/ToStatus carry the same old enum and need
            // the identical remap so the audit trail still reads correctly.
            migrationBuilder.Sql(
                "UPDATE [ClaimHistory] SET [FromStatus] = 7 WHERE [FromStatus] = 4; " +
                "UPDATE [ClaimHistory] SET [FromStatus] = 6 WHERE [FromStatus] = 3; " +
                "UPDATE [ClaimHistory] SET [FromStatus] = 4 WHERE [FromStatus] = 2; " +
                "UPDATE [ClaimHistory] SET [ToStatus] = 7 WHERE [ToStatus] = 4; " +
                "UPDATE [ClaimHistory] SET [ToStatus] = 6 WHERE [ToStatus] = 3; " +
                "UPDATE [ClaimHistory] SET [ToStatus] = 4 WHERE [ToStatus] = 2;");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "AfterSaleClaims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReopenCount",
                table: "AfterSaleClaims",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaTargetAt",
                table: "AfterSaleClaims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarrantyId",
                table: "AfterSaleClaims",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AfterSaleClaims_WarrantyId",
                table: "AfterSaleClaims",
                column: "WarrantyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AfterSaleClaims_WarrantyId",
                table: "AfterSaleClaims");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "AfterSaleClaims");

            migrationBuilder.DropColumn(
                name: "ReopenCount",
                table: "AfterSaleClaims");

            migrationBuilder.DropColumn(
                name: "SlaTargetAt",
                table: "AfterSaleClaims");

            migrationBuilder.DropColumn(
                name: "WarrantyId",
                table: "AfterSaleClaims");
        }
    }
}
