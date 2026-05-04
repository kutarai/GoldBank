using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    /// <summary>
    /// Introduces the Customer aggregate so that assets attach to a person, not an account.
    ///
    /// Strategy:
    ///   1. Create the customers table.
    ///   2. Add a nullable customer_id to accounts and assets.
    ///   3. Backfill customers from accounts (one row per distinct tenant+phone).
    ///   4. Wire accounts.customer_id and assets.customer_id from that backfill.
    ///   5. Drop the old assets.account_id column and its indexes.
    ///   6. Promote both customer_id columns to NOT NULL and add FKs.
    /// </summary>
    public partial class AddCustomersAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. customers table
            migrationBuilder.CreateTable(
                name: "customers",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone_country_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    date_of_birth = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    national_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_tenant_id",
                schema: "bank",
                table: "customers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_tenant_phone_unique",
                schema: "bank",
                table: "customers",
                columns: new[] { "tenant_id", "phone" },
                unique: true,
                filter: "deleted_at IS NULL");

            // 2. nullable customer_id on accounts and assets (assets keeps account_id for the
            //    backfill window — we drop it at the end of this migration).
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                schema: "bank",
                table: "accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                schema: "bank",
                table: "assets",
                type: "uuid",
                nullable: true);

            // 3. Backfill customers (one row per distinct tenant_id + phone in accounts).
            //    Use the most-recently-created account row to source personal fields.
            migrationBuilder.Sql(@"
                INSERT INTO bank.customers
                    (""Id"", phone, phone_country_code, first_name, last_name, email,
                     date_of_birth, national_id, tenant_id, status, created_at)
                SELECT
                    gen_random_uuid(),
                    src.phone,
                    src.phone_country_code,
                    src.first_name,
                    src.last_name,
                    src.email,
                    src.date_of_birth,
                    src.national_id,
                    src.tenant_id,
                    'active',
                    src.created_at
                FROM (
                    SELECT DISTINCT ON (tenant_id, phone)
                        tenant_id, phone, phone_country_code,
                        first_name, last_name, email, date_of_birth, national_id, created_at
                    FROM bank.accounts
                    WHERE deleted_at IS NULL
                    ORDER BY tenant_id, phone, created_at DESC
                ) src;
            ");

            // 4. Link accounts → customers.
            migrationBuilder.Sql(@"
                UPDATE bank.accounts a
                SET customer_id = c.""Id""
                FROM bank.customers c
                WHERE c.tenant_id = a.tenant_id
                  AND c.phone     = a.phone;
            ");

            // 5. Carry assets across via the accounts join, then drop the old FK & column.
            migrationBuilder.Sql(@"
                UPDATE bank.assets s
                SET customer_id = a.customer_id
                FROM bank.accounts a
                WHERE a.""Id"" = s.account_id;
            ");

            // 6. Promote NOT NULL.
            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id",
                schema: "bank",
                table: "accounts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "customer_id",
                schema: "bank",
                table: "assets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 7. Drop old assets indexes + column.
            migrationBuilder.DropIndex(
                name: "ix_assets_account_id",
                schema: "bank",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_assets_account_status",
                schema: "bank",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "account_id",
                schema: "bank",
                table: "assets");

            // 8. New indexes & FKs.
            migrationBuilder.CreateIndex(
                name: "ix_accounts_customer_id",
                schema: "bank",
                table: "accounts",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_customer_id",
                schema: "bank",
                table: "assets",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_customer_status",
                schema: "bank",
                table: "assets",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_customers_customer_id",
                schema: "bank",
                table: "accounts",
                column: "customer_id",
                principalSchema: "bank",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_assets_customers_customer_id",
                schema: "bank",
                table: "assets",
                column: "customer_id",
                principalSchema: "bank",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_assets_customers_customer_id",
                schema: "bank",
                table: "assets");

            migrationBuilder.DropForeignKey(
                name: "FK_accounts_customers_customer_id",
                schema: "bank",
                table: "accounts");

            migrationBuilder.AddColumn<Guid>(
                name: "account_id",
                schema: "bank",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "ix_assets_customer_status",
                schema: "bank",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_assets_customer_id",
                schema: "bank",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_accounts_customer_id",
                schema: "bank",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "bank",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "bank",
                table: "accounts");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "bank");

            migrationBuilder.CreateIndex(
                name: "ix_assets_account_id",
                schema: "bank",
                table: "assets",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_account_status",
                schema: "bank",
                table: "assets",
                columns: new[] { "account_id", "status" });
        }
    }
}
