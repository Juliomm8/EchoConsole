using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Severity = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InstallationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAlerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemAlerts_CreatedAtUtc",
                table: "SystemAlerts",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAlerts_IsResolved",
                table: "SystemAlerts",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAlerts_IsResolved_CreatedAtUtc",
                table: "SystemAlerts",
                columns: new[] { "IsResolved", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemAlerts");
        }
    }
}
