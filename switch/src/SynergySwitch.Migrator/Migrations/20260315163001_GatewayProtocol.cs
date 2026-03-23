using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SynergySwitch.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class GatewayProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Protocol",
                table: "Gateways",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Protocol",
                table: "Gateways");
        }
    }
}
