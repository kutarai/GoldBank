using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniBank.TerminalMigrator.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "terminal_mgmt");

            migrationBuilder.CreateTable(
                name: "terminal_key_infos",
                schema: "terminal_mgmt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_key_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    active_session_key_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_rotation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_rotation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_key_infos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "terminal_updates",
                schema: "terminal_mgmt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    update_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pushed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_updates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "terminals",
                schema: "terminal_mgmt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    firmware_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    mqtt_topic_prefix = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    last_heartbeat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_key_injection = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_terminal_key_infos_terminal",
                schema: "terminal_mgmt",
                table: "terminal_key_infos",
                column: "terminal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_terminal_updates_terminal",
                schema: "terminal_mgmt",
                table: "terminal_updates",
                column: "terminal_id");

            migrationBuilder.CreateIndex(
                name: "ix_terminal_updates_terminal_status",
                schema: "terminal_mgmt",
                table: "terminal_updates",
                columns: new[] { "terminal_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_terminals_merchant",
                schema: "terminal_mgmt",
                table: "terminals",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ix_terminals_serial",
                schema: "terminal_mgmt",
                table: "terminals",
                column: "serial_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_terminals_status",
                schema: "terminal_mgmt",
                table: "terminals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_terminals_tenant",
                schema: "terminal_mgmt",
                table: "terminals",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terminal_key_infos",
                schema: "terminal_mgmt");

            migrationBuilder.DropTable(
                name: "terminal_updates",
                schema: "terminal_mgmt");

            migrationBuilder.DropTable(
                name: "terminals",
                schema: "terminal_mgmt");
        }
    }
}
