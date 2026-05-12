using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EchoConsole.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendInstallationsInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OperatingSystem",
                table: "Installations",
                newName: "OSVersion");

            migrationBuilder.RenameColumn(
                name: "LastSeenUtc",
                table: "Installations",
                newName: "LastUpdateUtc");

            migrationBuilder.AddColumn<string>(
                name: "Gpu",
                table: "Installations",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Processor",
                table: "Installations",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RamMb",
                table: "Installations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Installations_DeviceName",
                table: "Installations",
                column: "DeviceName");

            migrationBuilder.CreateIndex(
                name: "IX_Installations_LastUpdateUtc",
                table: "Installations",
                column: "LastUpdateUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Installations_DeviceName",
                table: "Installations");

            migrationBuilder.DropIndex(
                name: "IX_Installations_LastUpdateUtc",
                table: "Installations");

            migrationBuilder.DropColumn(
                name: "Gpu",
                table: "Installations");

            migrationBuilder.DropColumn(
                name: "Processor",
                table: "Installations");

            migrationBuilder.DropColumn(
                name: "RamMb",
                table: "Installations");

            migrationBuilder.RenameColumn(
                name: "OSVersion",
                table: "Installations",
                newName: "OperatingSystem");

            migrationBuilder.RenameColumn(
                name: "LastUpdateUtc",
                table: "Installations",
                newName: "LastSeenUtc");
        }
    }
}
