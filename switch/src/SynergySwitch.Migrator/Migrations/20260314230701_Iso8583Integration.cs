using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SynergySwitch.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class Iso8583Integration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileMoneyPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TerminalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MerchantId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorizationCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ProviderReference = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileMoneyPayments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QrPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TerminalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MerchantId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    QrPayload = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorizationCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ProviderReference = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrPayments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileMoneyPayments_PaymentReference",
                table: "MobileMoneyPayments",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileMoneyPayments_Status",
                table: "MobileMoneyPayments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MobileMoneyPayments_TerminalId",
                table: "MobileMoneyPayments",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_QrPayments_PaymentReference",
                table: "QrPayments",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrPayments_Status",
                table: "QrPayments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QrPayments_TerminalId",
                table: "QrPayments",
                column: "TerminalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileMoneyPayments");

            migrationBuilder.DropTable(
                name: "QrPayments");
        }
    }
}
