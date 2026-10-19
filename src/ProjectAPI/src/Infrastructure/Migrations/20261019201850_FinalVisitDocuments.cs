using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalVisitDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinalVisitDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SnagId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalVisitDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinalVisitDocuments_FinalVisitCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "FinalVisitCases",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinalVisitDocuments_FinalVisitReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "FinalVisitReports",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FinalVisitDocuments_Snags_SnagId",
                        column: x => x.SnagId,
                        principalTable: "Snags",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinalVisitDocuments_CaseKind",
                table: "FinalVisitDocuments",
                columns: new[] { "CaseId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_FinalVisitDocuments_ReportId",
                table: "FinalVisitDocuments",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_FinalVisitDocuments_Snag",
                table: "FinalVisitDocuments",
                column: "SnagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinalVisitDocuments");
        }
    }
}
