using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InternalInvitationProjectAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalInvitationProjectAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalInvitationProjectAssignments_InternalInvitations_InternalInvitationId",
                        column: x => x.InternalInvitationId,
                        principalTable: "InternalInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InternalInvitationProjectAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalInvitationProjectAssignments_InvitationId_ProjectId",
                table: "InternalInvitationProjectAssignments",
                columns: new[] { "InternalInvitationId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalInvitationProjectAssignments_ProjectId",
                table: "InternalInvitationProjectAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalInvitations_Email",
                table: "InternalInvitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_InternalInvitations_TokenHash",
                table: "InternalInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalInvitationProjectAssignments");

            migrationBuilder.DropTable(
                name: "InternalInvitations");
        }
    }
}
