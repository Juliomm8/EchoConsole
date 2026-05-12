using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameBuilds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameBuilds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReleaseNotes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReleaseDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameBuilds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameBuilds_ReleaseDateUtc",
                table: "GameBuilds",
                column: "ReleaseDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GameBuilds_VersionNumber",
                table: "GameBuilds",
                column: "VersionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameBuilds");
        }
    }
}
