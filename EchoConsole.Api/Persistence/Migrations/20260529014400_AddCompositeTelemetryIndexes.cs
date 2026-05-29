using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeTelemetryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameSessions_InstallationDbId",
                table: "GameSessions");

            migrationBuilder.CreateIndex(
                name: "IX_Installations_OwnerUserId_LastUpdateUtc",
                table: "Installations",
                columns: new[] { "OwnerUserId", "LastUpdateUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_InstallationDbId_LastHeartbeatUtc",
                table: "GameSessions",
                columns: new[] { "InstallationDbId", "LastHeartbeatUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_InstallationDbId_StartedAtUtc",
                table: "GameSessions",
                columns: new[] { "InstallationDbId", "StartedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_Status_EndedAtUtc_LastHeartbeatUtc",
                table: "GameSessions",
                columns: new[] { "Status", "EndedAtUtc", "LastHeartbeatUtc" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Installations_OwnerUserId_LastUpdateUtc",
                table: "Installations");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_InstallationDbId_LastHeartbeatUtc",
                table: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_InstallationDbId_StartedAtUtc",
                table: "GameSessions");

            migrationBuilder.DropIndex(
                name: "IX_GameSessions_Status_EndedAtUtc_LastHeartbeatUtc",
                table: "GameSessions");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_InstallationDbId",
                table: "GameSessions",
                column: "InstallationDbId");
        }
    }
}
