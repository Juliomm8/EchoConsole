using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIngest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Installations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BuildVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DeviceModel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OperatingSystem = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationDbId = table.Column<int>(type: "int", nullable: false),
                    SessionTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BuildVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentScene = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CurrentGameState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrentPhase = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_Installations_InstallationDbId",
                        column: x => x.InstallationDbId,
                        principalTable: "Installations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_InstallationDbId",
                table: "GameSessions",
                column: "InstallationDbId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_LastHeartbeatUtc",
                table: "GameSessions",
                column: "LastHeartbeatUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_SessionId",
                table: "GameSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Installations_InstallationId",
                table: "Installations",
                column: "InstallationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "Installations");
        }
    }
}
