using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    public partial class AddCurrencyDenominations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currency_denominations",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    face_value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    denomination_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency_denominations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_currency_denominations_tenant_ccy_face",
                schema: "bank",
                table: "currency_denominations",
                columns: new[] { "tenant_id", "currency", "face_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currency_denominations_lookup",
                schema: "bank",
                table: "currency_denominations",
                columns: new[] { "tenant_id", "currency", "is_active", "display_order" });

            migrationBuilder.Sql(@"
INSERT INTO bank.currency_denominations (""Id"", tenant_id, currency, face_value, denomination_type, display_order, is_active, created_at)
VALUES
  (gen_random_uuid(), 'unibank', 'USD', 100,    'Note', 1,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',  50,    'Note', 2,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',  20,    'Note', 3,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',  10,    'Note', 4,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',   5,    'Note', 5,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',   1,    'Note', 6,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',   0.50, 'Coin', 7,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',   0.25, 'Coin', 8,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',   0.10, 'Coin', 9,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',   0.05, 'Coin', 10, true, NOW()),
  (gen_random_uuid(), 'unibank', 'USD',   0.01, 'Coin', 11, true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG', 200,    'Note', 1,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG', 100,    'Note', 2,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',  50,    'Note', 3,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',  20,    'Note', 4,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',  10,    'Note', 5,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',   5,    'Note', 6,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',   2,    'Note', 7,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',   1,    'Note', 8,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',   0.50, 'Coin', 9,  true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',   0.25, 'Coin', 10, true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',   0.10, 'Coin', 11, true, NOW()),
  (gen_random_uuid(), 'unibank', 'ZWG',   0.05, 'Coin', 12, true, NOW());
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "currency_denominations",
                schema: "bank");
        }
    }
}
