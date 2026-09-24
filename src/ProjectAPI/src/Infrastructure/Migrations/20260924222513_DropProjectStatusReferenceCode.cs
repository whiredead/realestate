using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// A project has one status: StatusGlobal (sur plan / en livraison / finalisé), the phase that drives the
    /// business rules. The second, free "statut" label could disagree with it (a sur-plan project labelled
    /// "En livraison"), so it is removed. StatusGlobal is untouched. Down re-creates the column from it.
    /// </remarks>
    public partial class DropProjectStatusReferenceCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusReferenceCode",
                table: "Projects");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusReferenceCode",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("UPDATE Projects SET StatusReferenceCode = StatusGlobal;");
        }
    }
}
