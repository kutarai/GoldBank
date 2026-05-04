using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.PublicDb
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bank");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    SchemaName = table.Column<string>(type: "text", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    BrandingJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    Timezone = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "bank",
                table: "outbox_messages",
                columns: new[] { "ProcessedAt", "RetryCount", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Code",
                schema: "bank",
                table: "tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_SchemaName",
                schema: "bank",
                table: "tenants",
                column: "SchemaName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "bank");
        }
    }
}
