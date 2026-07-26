using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryContactId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CrmContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Cin = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EmailNormalized = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PhoneNormalized = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AcquisitionSourceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OwnerSalesAgentId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmContacts", x => x.Id);
                    table.CheckConstraint("CK_CrmContacts_LifecycleStatus", "[LifecycleStatus] IN ('PROSPECT','BUYER','DELIVERED_OWNER','ARCHIVED')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PrimaryContactId",
                table: "Reservations",
                column: "PrimaryContactId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmContacts_ContactNumber",
                table: "CrmContacts",
                column: "ContactNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmContacts_EmailNormalized",
                table: "CrmContacts",
                column: "EmailNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_CrmContacts_PhoneNormalized",
                table: "CrmContacts",
                column: "PhoneNormalized");

            migrationBuilder.CreateIndex(
                name: "UX_CrmContacts_UserId",
                table: "CrmContacts",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_CrmContacts_PrimaryContactId",
                table: "Reservations",
                column: "PrimaryContactId",
                principalTable: "CrmContacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_CrmContacts_PrimaryContactId",
                table: "Reservations");

            migrationBuilder.DropTable(
                name: "CrmContacts");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_PrimaryContactId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PrimaryContactId",
                table: "Reservations");
        }
    }
}
