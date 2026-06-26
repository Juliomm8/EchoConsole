using System;
using EchoConsole.Api.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations;

[DbContext(typeof(EchoConsoleDbContext))]
[Migration("20260625235900_AddPlayerProfilePreferencesAndUserSessions")]
public sealed class AddPlayerProfilePreferencesAndUserSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreferredLanguage",
            table: "Users",
            type: "nvarchar(8)",
            maxLength: 8,
            nullable: false,
            defaultValue: "en");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ProfileUpdatedAtUtc",
            table: "Users",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "UserSessions",
            columns: table => new
            {
                Id = table.Column<long>(
                        type: "bigint",
                        nullable: false)
                    .Annotation(
                        "SqlServer:Identity",
                        "1, 1"),
                UserId = table.Column<int>(
                    type: "int",
                    nullable: false),
                SessionKeyHash = table.Column<string>(
                    type: "nvarchar(64)",
                    maxLength: 64,
                    nullable: false),
                UserAgent = table.Column<string>(
                    type: "nvarchar(512)",
                    maxLength: 512,
                    nullable: false),
                MaskedIpAddress = table.Column<string>(
                    type: "nvarchar(64)",
                    maxLength: 64,
                    nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false),
                LastSeenAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(
                    type: "datetimeoffset",
                    nullable: true),
                RevokedReason = table.Column<string>(
                    type: "nvarchar(128)",
                    maxLength: 128,
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_UserSessions",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_UserSessions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_ExpiresAtUtc",
            table: "UserSessions",
            column: "ExpiresAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_SessionKeyHash",
            table: "UserSessions",
            column: "SessionKeyHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserId",
            table: "UserSessions",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserId_RevokedAtUtc_LastSeenAtUtc",
            table: "UserSessions",
            columns: new[]
            {
                "UserId",
                "RevokedAtUtc",
                "LastSeenAtUtc"
            },
            descending: new[]
            {
                false,
                false,
                true
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserSessions");

        migrationBuilder.DropColumn(
            name: "PreferredLanguage",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "ProfileUpdatedAtUtc",
            table: "Users");
    }
}
