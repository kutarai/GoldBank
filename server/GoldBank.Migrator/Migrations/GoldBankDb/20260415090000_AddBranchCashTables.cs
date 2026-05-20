using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.GoldBankDb
{
    /// <inheritdoc />
    public partial class AddBranchCashTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teller_drawer_sessions",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    teller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opening_float_json = table.Column<string>(type: "jsonb", nullable: false),
                    closing_balance_json = table.Column<string>(type: "jsonb", nullable: true),
                    expected_closing_json = table.Column<string>(type: "jsonb", nullable: true),
                    variance_json = table.Column<string>(type: "jsonb", nullable: true),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by_supervisor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teller_drawer_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "branch_cash_transactions",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    drawer_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    depositor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    denomination_breakdown_json = table.Column<string>(type: "jsonb", nullable: false),
                    identity_verified = table.Column<bool>(type: "boolean", nullable: false),
                    supervisor_approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supervisor_approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    receipt_pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reversed_by_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_cash_transactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_teller_drawer_sessions_branch_id",
                schema: "bank",
                table: "teller_drawer_sessions",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_teller_drawer_sessions_status",
                schema: "bank",
                table: "teller_drawer_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_teller_drawer_sessions_teller_date",
                schema: "bank",
                table: "teller_drawer_sessions",
                columns: new[] { "teller_id", "business_date" });

            migrationBuilder.CreateIndex(
                name: "ix_branch_cash_transactions_account_created",
                schema: "bank",
                table: "branch_cash_transactions",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_branch_cash_transactions_drawer",
                schema: "bank",
                table: "branch_cash_transactions",
                column: "drawer_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_branch_cash_transactions_status",
                schema: "bank",
                table: "branch_cash_transactions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_branch_cash_transactions_transaction_id",
                schema: "bank",
                table: "branch_cash_transactions",
                column: "transaction_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_cash_transactions",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "teller_drawer_sessions",
                schema: "bank");
        }
    }
}
