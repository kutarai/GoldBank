using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SynergySwitch.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class GatewayOfflineMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OfflineMode",
                table: "Gateways",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfflineMode",
                table: "Gateways");
        }
    }
}
