using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations;

[DbContext(typeof(EchoConsoleDbContext))]
[Migration("20260625000000_AddAlertTypeCatalogAndDiscordOutbox")]
public sealed class AddAlertTypeCatalogAndDiscordOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AlertTypeDefinitions",
            columns: table => new
            {
                Id = table.Column<int>(
                        type: "int",
                        nullable: false)
                    .Annotation(
                        "SqlServer:Identity",
                        "1, 1"),
                Code = table.Column<string>(
                    type: "nvarchar(64)",
                    maxLength: 64,
                    nullable: false),
                Name = table.Column<string>(
                    type: "nvarchar(128)",
                    maxLength: 128,
                    nullable: false),
                Description = table.Column<string>(
                    type: "nvarchar(500)",
                    maxLength: 500,
                    nullable: false),
                DefaultSeverity = table.Column<string>(
                    type: "nvarchar(24)",
                    maxLength: 24,
                    nullable: false),
                IsActive = table.Column<bool>(
                    type: "bit",
                    nullable: false,
                    defaultValue: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_AlertTypeDefinitions",
                    x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertTypeDefinitions_Code",
            table: "AlertTypeDefinitions",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AlertTypeDefinitions_IsActive",
            table: "AlertTypeDefinitions",
            column: "IsActive");

        var seededAtUtc = new DateTimeOffset(
            2026,
            6,
            25,
            0,
            0,
            0,
            TimeSpan.Zero);

        migrationBuilder.InsertData(
            table: "AlertTypeDefinitions",
            columns: new[]
            {
                "Id",
                "Code",
                "Name",
                "Description",
                "DefaultSeverity",
                "IsActive",
                "CreatedAtUtc",
                "UpdatedAtUtc"
            },
            columnTypes: new[]
            {
                "int",
                "nvarchar(64)",
                "nvarchar(128)",
                "nvarchar(500)",
                "nvarchar(24)",
                "bit",
                "datetimeoffset",
                "datetimeoffset"
            },
            values: new object[,]
            {
                {
                    1,
                    "UNCLASSIFIED",
                    "Unclassified telemetry anomaly",
                    "Fallback category for legacy alerts and removed error definitions.",
                    "Warning",
                    true,
                    seededAtUtc,
                    seededAtUtc
                },
                {
                    2,
                    "NETWORK_DISCONNECT",
                    "Network disconnect",
                    "Connection loss, timeout, packet starvation, or telemetry transport interruption.",
                    "Critical",
                    true,
                    seededAtUtc,
                    seededAtUtc
                },
                {
                    3,
                    "RENDER_PIPELINE_FAULT",
                    "Render pipeline fault",
                    "Failure in the rendering pipeline, shader compilation, frame composition, or graphics device state.",
                    "Critical",
                    true,
                    seededAtUtc,
                    seededAtUtc
                },
                {
                    4,
                    "DATABASE_TIMEOUT",
                    "Database timeout",
                    "Database command timeout, retry exhaustion, connection pool pressure, or transaction latency anomaly.",
                    "Critical",
                    true,
                    seededAtUtc,
                    seededAtUtc
                },
                {
                    5,
                    "PHYSICS_COLLIDER_CRASH",
                    "Physics collider crash",
                    "Unrecoverable collider, rigidbody, or physics simulation fault reported by the Unity client.",
                    "Fatal",
                    true,
                    seededAtUtc,
                    seededAtUtc
                }
            });

        migrationBuilder.AddColumn<string>(
            name: "BuildVersion",
            table: "SystemAlerts",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ErrorTypeCode",
            table: "SystemAlerts",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "UNCLASSIFIED");

        migrationBuilder.CreateIndex(
            name: "IX_SystemAlerts_BuildVersion",
            table: "SystemAlerts",
            column: "BuildVersion");

        migrationBuilder.CreateIndex(
            name: "IX_SystemAlerts_ErrorTypeCode",
            table: "SystemAlerts",
            column: "ErrorTypeCode");

        migrationBuilder.CreateIndex(
            name: "IX_SystemAlerts_IsResolved_Severity_CreatedAtUtc",
            table: "SystemAlerts",
            columns: new[]
            {
                "IsResolved",
                "Severity",
                "CreatedAtUtc"
            },
            descending: new[]
            {
                false,
                false,
                true
            });


        migrationBuilder.CreateTable(
            name: "AlertDiscordOutboxMessages",
            columns: table => new
            {
                Id = table.Column<long>(
                        type: "bigint",
                        nullable: false)
                    .Annotation(
                        "SqlServer:Identity",
                        "1, 1"),
                SystemAlertId = table.Column<int>(
                    type: "int",
                    nullable: false),
                EnqueuedAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false),
                AttemptCount = table.Column<int>(
                    type: "int",
                    nullable: false),
                NextAttemptUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false),
                SentAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: true),
                LastError = table.Column<string>(
                    type: "nvarchar(1000)",
                    maxLength: 1000,
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_AlertDiscordOutboxMessages",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_AlertDiscordOutboxMessages_SystemAlerts_SystemAlertId",
                    column: x => x.SystemAlertId,
                    principalTable: "SystemAlerts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertDiscordOutboxMessages_SentAtUtc_NextAttemptUtc",
            table: "AlertDiscordOutboxMessages",
            columns: new[]
            {
                "SentAtUtc",
                "NextAttemptUtc"
            });

        migrationBuilder.CreateIndex(
            name: "IX_AlertDiscordOutboxMessages_SystemAlertId",
            table: "AlertDiscordOutboxMessages",
            column: "SystemAlertId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AlertDiscordOutboxMessages");


        migrationBuilder.DropIndex(
            name: "IX_SystemAlerts_BuildVersion",
            table: "SystemAlerts");

        migrationBuilder.DropIndex(
            name: "IX_SystemAlerts_ErrorTypeCode",
            table: "SystemAlerts");

        migrationBuilder.DropIndex(
            name: "IX_SystemAlerts_IsResolved_Severity_CreatedAtUtc",
            table: "SystemAlerts");

        migrationBuilder.DropColumn(
            name: "BuildVersion",
            table: "SystemAlerts");

        migrationBuilder.DropColumn(
            name: "ErrorTypeCode",
            table: "SystemAlerts");

        migrationBuilder.DropTable(
            name: "AlertTypeDefinitions");
    }
}
