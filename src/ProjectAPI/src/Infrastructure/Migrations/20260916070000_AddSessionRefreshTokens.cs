using System;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace ProjectAPI.Infrastructure.Migrations;
public partial class AddSessionRefreshTokens : Migration
{
 protected override void Up(MigrationBuilder m) { m.CreateTable(name:"SessionRefreshTokens",columns:table=>new { Id=table.Column<Guid>(type:"uniqueidentifier",nullable:false),UserId=table.Column<string>(type:"nvarchar(450)",maxLength:450,nullable:false),TokenHash=table.Column<string>(type:"nvarchar(64)",maxLength:64,nullable:false),ExpiresAtUtc=table.Column<DateTime>(type:"datetime2",nullable:false),RevokedAtUtc=table.Column<DateTime>(type:"datetime2",nullable:true)},constraints:table=>table.PrimaryKey("PK_SessionRefreshTokens",x=>x.Id));m.CreateIndex(name:"IX_SessionRefreshTokens_TokenHash",table:"SessionRefreshTokens",column:"TokenHash",unique:true); }
 protected override void Down(MigrationBuilder m)=>m.DropTable(name:"SessionRefreshTokens");
}
