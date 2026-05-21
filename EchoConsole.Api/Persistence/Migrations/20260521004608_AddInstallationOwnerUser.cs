using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallationOwnerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "Installations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Installations_OwnerUserId",
                table: "Installations",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Installations_Users_OwnerUserId",
                table: "Installations",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Installations_Users_OwnerUserId",
                table: "Installations");

            migrationBuilder.DropIndex(
                name: "IX_Installations_OwnerUserId",
                table: "Installations");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Installations");
        }
    }
}
