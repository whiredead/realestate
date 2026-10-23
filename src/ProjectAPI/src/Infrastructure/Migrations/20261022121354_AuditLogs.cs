using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorRoles = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogSubjects_AuditLogs_AuditLogId",
                        column: x => x.AuditLogId,
                        principalTable: "AuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Category_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "Category", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ProjectId_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "ProjectId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogSubjects_AuditLogId",
                table: "AuditLogSubjects",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogSubjects_EntityType_EntityId",
                table: "AuditLogSubjects",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogSubjects");

            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
