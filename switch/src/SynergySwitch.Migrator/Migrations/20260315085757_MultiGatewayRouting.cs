using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SynergySwitch.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class MultiGatewayRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GatewayAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GatewayId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gateways",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    AcquiringInstitutionId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NetworkId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PoolSize = table.Column<int>(type: "integer", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    SendLengthHeader = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gateways", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatewayBinRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GatewayId = table.Column<int>(type: "integer", nullable: false),
                    BinPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayBinRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatewayBinRoutes_Gateways_GatewayId",
                        column: x => x.GatewayId,
                        principalTable: "Gateways",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GatewayAuditLogs_GatewayId",
                table: "GatewayAuditLogs",
                column: "GatewayId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayAuditLogs_Timestamp",
                table: "GatewayAuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayBinRoutes_GatewayId_BinPrefix",
                table: "GatewayBinRoutes",
                columns: new[] { "GatewayId", "BinPrefix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gateways_Name",
                table: "Gateways",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GatewayAuditLogs");

            migrationBuilder.DropTable(
                name: "GatewayBinRoutes");

            migrationBuilder.DropTable(
                name: "Gateways");
        }
    }
}
