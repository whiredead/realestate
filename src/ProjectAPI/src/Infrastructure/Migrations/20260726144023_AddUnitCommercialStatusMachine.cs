using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitCommercialStatusMachine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Units",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // The column previously held the legacy PascalCase spellings
            // ("Available", "Reserved", "Sold"). Normalise them to the canonical
            // §3 codes BEFORE the CHECK constraint below is added, otherwise the
            // constraint cannot be created on a populated database.
            //
            // The target database is currently empty, so this is a no-op today —
            // it is here so the migration remains correct if it is ever replayed
            // against an environment that has data.
            migrationBuilder.Sql(@"
                UPDATE [Units] SET [Status] = 'AVAILABLE'             WHERE [Status] IN ('Available', 'available');
                UPDATE [Units] SET [Status] = 'RESERVED'              WHERE [Status] IN ('Reserved', 'reserved');
                UPDATE [Units] SET [Status] = 'SOLD'                  WHERE [Status] IN ('Sold', 'sold');
                UPDATE [Units] SET [Status] = 'HOLD_PENDING_APPROVAL' WHERE [Status] = 'HoldPendingApproval';
                UPDATE [Units] SET [Status] = 'CONTRACTED'            WHERE [Status] = 'Contracted';
                UPDATE [Units] SET [Status] = 'DELIVERED'             WHERE [Status] = 'Delivered';
                UPDATE [Units] SET [Status] = 'SUSPENDED'             WHERE [Status] = 'Suspended';
                UPDATE [Units] SET [Status] = 'CANCELLED'             WHERE [Status] = 'Cancelled';
                UPDATE [Units] SET [Status] = 'AVAILABLE'             WHERE [Status] IS NULL OR LTRIM(RTRIM([Status])) = '';
            ");

            migrationBuilder.CreateTable(
                name: "UnitStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cause = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitStatusHistories_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Units_Status",
                table: "Units",
                sql: "[Status] IN ('AVAILABLE', 'HOLD_PENDING_APPROVAL', 'RESERVED', 'CONTRACTED', 'SOLD', 'DELIVERED', 'SUSPENDED', 'CANCELLED')");

            migrationBuilder.CreateIndex(
                name: "IX_UnitStatusHistories_UnitDate",
                table: "UnitStatusHistories",
                columns: new[] { "UnitId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitStatusHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Units_Status",
                table: "Units");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Units",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);
        }
    }
}
