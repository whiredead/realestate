using System;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace ProjectAPI.Infrastructure.Migrations;
public partial class AddReferenceItems : Migration
{
 protected override void Up(MigrationBuilder m) { m.CreateTable(name:"ReferenceItems", columns: table => new { Id=table.Column<Guid>(type:"uniqueidentifier",nullable:false), Category=table.Column<string>(type:"nvarchar(80)",maxLength:80,nullable:false), Code=table.Column<string>(type:"nvarchar(80)",maxLength:80,nullable:false), Label=table.Column<string>(type:"nvarchar(160)",maxLength:160,nullable:false), Description=table.Column<string>(type:"nvarchar(2000)",maxLength:2000,nullable:true), SortOrder=table.Column<int>(type:"int",nullable:false), IsActive=table.Column<bool>(type:"bit",nullable:false) }, constraints: table => table.PrimaryKey("PK_ReferenceItems",x=>x.Id)); m.CreateIndex(name:"IX_ReferenceItems_Category_Code",table:"ReferenceItems",columns:new[]{"Category","Code"},unique:true); m.CreateIndex(name:"IX_ReferenceItems_Category_SortOrder",table:"ReferenceItems",columns:new[]{"Category","SortOrder"}); }
 protected override void Down(MigrationBuilder m) => m.DropTable(name:"ReferenceItems");
}
