using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SynergySwitch.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Merchants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MerchantId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryCode = table.Column<string>(type: "text", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Merchants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Terminals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TerminalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MerchantId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SerialNumber = table.Column<string>(type: "text", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "text", nullable: true),
                    AppVersion = table.Column<string>(type: "text", nullable: true),
                    LastHeartbeat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BatteryLevel = table.Column<int>(type: "integer", nullable: false),
                    TransactionCount = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terminals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExchangeId = table.Column<string>(type: "text", nullable: false),
                    TransactionReference = table.Column<string>(type: "text", nullable: true),
                    TerminalId = table.Column<string>(type: "text", nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    PanLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CardEntryMode = table.Column<string>(type: "text", nullable: false),
                    CvmMethod = table.Column<string>(type: "text", nullable: false),
                    ResponseCode = table.Column<string>(type: "text", nullable: false),
                    ResponseReason = table.Column<string>(type: "text", nullable: false),
                    AuthorisationCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RequestTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponseTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasIccData = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Merchants_MerchantId",
                table: "Merchants",
                column: "MerchantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Terminals_TerminalId",
                table: "Terminals",
                column: "TerminalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_ExchangeId",
                table: "TransactionLogs",
                column: "ExchangeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_RequestTimestamp",
                table: "TransactionLogs",
                column: "RequestTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_ResponseCode",
                table: "TransactionLogs",
                column: "ResponseCode");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLogs_TerminalId",
                table: "TransactionLogs",
                column: "TerminalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Merchants");

            migrationBuilder.DropTable(
                name: "Terminals");

            migrationBuilder.DropTable(
                name: "TransactionLogs");
        }
    }
}
