using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReservationContractAndPaymentProofs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContractIssuedAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractIssuedBy",
                table: "Reservations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractSignedAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractSignedBy",
                table: "Reservations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractStatus",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "NotaryAppointmentsOpen",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProofId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentProofs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentProofs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentProofs_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Proof",
                table: "Payments",
                column: "ProofId",
                unique: true,
                filter: "[ProofId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_Reservation",
                table: "PaymentProofs",
                column: "ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentProofs_ProofId",
                table: "Payments",
                column: "ProofId",
                principalTable: "PaymentProofs",
                principalColumn: "Id");

            // The agent no longer attaches the contract at submission: it is filed
            // by the sales admin after approval. Kept as an optional checklist item
            // rather than deleted, so a project admin can still see and remove it.
            migrationBuilder.Sql(
                "UPDATE [ProjectDocumentRequirements] SET [IsRequired] = 0 WHERE [DocumentType] = 'RESERVATION_CONTRACT';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [ProjectDocumentRequirements] SET [IsRequired] = 1 WHERE [DocumentType] = 'RESERVATION_CONTRACT';");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentProofs_ProofId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "PaymentProofs");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Proof",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ContractIssuedAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContractIssuedBy",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContractSignedAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContractSignedBy",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContractStatus",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "NotaryAppointmentsOpen",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProofId",
                table: "Payments");
        }
    }
}
