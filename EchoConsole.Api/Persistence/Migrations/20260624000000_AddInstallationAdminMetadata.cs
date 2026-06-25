using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations;

[DbContext(typeof(EchoConsoleDbContext))]
[Migration("20260625010000_AddInstallationAdminMetadata")]
public sealed class AddInstallationAdminMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AdminAlias",
            table: "Installations",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdminStatus",
            table: "Installations",
            type: "nvarchar(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "Active");

        migrationBuilder.CreateIndex(
            name: "IX_Installations_AdminAlias",
            table: "Installations",
            column: "AdminAlias");

        migrationBuilder.CreateIndex(
            name: "IX_Installations_AdminStatus",
            table: "Installations",
            column: "AdminStatus");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Installations_AdminAlias",
            table: "Installations");

        migrationBuilder.DropIndex(
            name: "IX_Installations_AdminStatus",
            table: "Installations");

        migrationBuilder.DropColumn(
            name: "AdminAlias",
            table: "Installations");

        migrationBuilder.DropColumn(
            name: "AdminStatus",
            table: "Installations");
    }
}
