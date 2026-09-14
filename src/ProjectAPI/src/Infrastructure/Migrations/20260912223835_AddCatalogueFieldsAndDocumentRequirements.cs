using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <summary>
    /// §7.2 / §12.1 — a per-layout 3D tour and shower-room count on TypeBien,
    /// plus the per-project reservation document requirements.
    ///
    /// Both TypeBien columns are nullable with no back-fill: existing rows keep
    /// NULL, which reads as "not recorded" everywhere the catalogue renders
    /// optional fields. 0 showers and "nobody has filled this in" are different
    /// statements and only the second is true of the existing data.
    ///
    /// ProjectDocumentRequirements is created EMPTY and stays empty until an
    /// administrator configures a project, and that is the whole safety
    /// property: "no rows" means "nothing required", so no existing project
    /// starts rejecting reservation submissions the moment this is applied.
    /// Nothing here seeds a default list, deliberately.
    /// </summary>
    public partial class AddCatalogueFieldsAndDocumentRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Module3DLink",
                table: "TypeBiens",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NbrDouche",
                table: "TypeBiens",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectDocumentRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LabelFr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDocumentRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDocumentRequirements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocumentRequirements_ProjectType",
                table: "ProjectDocumentRequirements",
                columns: new[] { "ProjectId", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDocumentRequirements");

            migrationBuilder.DropColumn(
                name: "Module3DLink",
                table: "TypeBiens");

            migrationBuilder.DropColumn(
                name: "NbrDouche",
                table: "TypeBiens");
        }
    }
}
