using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.GoldBankDb
{
    /// <inheritdoc />
    public partial class AddCardTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "card_transactions",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    merchant_id = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    merchant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    transaction_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    authorization_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    retrieval_reference = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    stan = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    terminal_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    processing_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    source_institution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    acquiring_institution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_transactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_account_created",
                schema: "bank",
                table: "card_transactions",
                columns: new[] { "account_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_reference",
                schema: "bank",
                table: "card_transactions",
                column: "reference");

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_retrieval_ref",
                schema: "bank",
                table: "card_transactions",
                column: "retrieval_reference");

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_stan_source",
                schema: "bank",
                table: "card_transactions",
                columns: new[] { "stan", "source_institution" });

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_tenant_created",
                schema: "bank",
                table: "card_transactions",
                columns: new[] { "tenant_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "card_transactions",
                schema: "bank");
        }
    }
}
