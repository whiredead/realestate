using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationPriceSnapshotAndBuyers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CatalogPrice",
                table: "Reservations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "Reservations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalPrice",
                table: "Reservations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerSalesAgentId",
                table: "Reservations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReservationBuyers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CrmContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnershipPercent = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationBuyers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationBuyers_CrmContacts_CrmContactId",
                        column: x => x.CrmContactId,
                        principalTable: "CrmContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationBuyers_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationBuyers_CrmContactId",
                table: "ReservationBuyers",
                column: "CrmContactId");

            migrationBuilder.CreateIndex(
                name: "UX_ReservationBuyers_ReservationContact",
                table: "ReservationBuyers",
                columns: new[] { "ReservationId", "CrmContactId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationBuyers");

            migrationBuilder.DropColumn(
                name: "CatalogPrice",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "FinalPrice",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "OwnerSalesAgentId",
                table: "Reservations");
        }
    }
}
