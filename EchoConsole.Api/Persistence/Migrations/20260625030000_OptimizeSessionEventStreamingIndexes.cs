using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations;

[DbContext(typeof(EchoConsoleDbContext))]
[Migration("20260625030000_OptimizeSessionEventStreamingIndexes")]
public sealed class OptimizeSessionEventStreamingIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_GameSessions_BuildVersion",
            table: "GameSessions",
            column: "BuildVersion");

        migrationBuilder.CreateIndex(
            name: "IX_GameSessionEvents_CreatedAtUtc",
            table: "GameSessionEvents",
            column: "CreatedAtUtc",
            descending: new[] { true });

        migrationBuilder.CreateIndex(
            name: "IX_GameSessionEvents_EventType_CreatedAtUtc",
            table: "GameSessionEvents",
            columns: new[]
            {
                "EventType",
                "CreatedAtUtc"
            },
            descending: new[]
            {
                false,
                true
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GameSessions_BuildVersion",
            table: "GameSessions");

        migrationBuilder.DropIndex(
            name: "IX_GameSessionEvents_CreatedAtUtc",
            table: "GameSessionEvents");

        migrationBuilder.DropIndex(
            name: "IX_GameSessionEvents_EventType_CreatedAtUtc",
            table: "GameSessionEvents");
    }
}
