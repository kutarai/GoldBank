using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    public partial class AddEkubLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ekub_loans",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    interest_rate_percent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    term_months = table.Column<int>(type: "integer", nullable: false),
                    total_repayable = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    installment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    outstanding_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_interest_earned = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    treasurer_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    disbursed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ekub_loans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ekub_loan_repayments",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    treasurer_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    principal_portion = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    interest_portion = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ekub_loan_repayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ekub_loan_repayments_ekub_loans_loan_id",
                        column: x => x.loan_id,
                        principalSchema: "bank",
                        principalTable: "ekub_loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ekub_loan_votes",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approve = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ekub_loan_votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ekub_loan_votes_ekub_loans_loan_id",
                        column: x => x.loan_id,
                        principalSchema: "bank",
                        principalTable: "ekub_loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ekub_loan_repayments_group",
                schema: "bank",
                table: "ekub_loan_repayments",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_loan_repayments_loan",
                schema: "bank",
                table: "ekub_loan_repayments",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_loan_votes_loan_voter_unique",
                schema: "bank",
                table: "ekub_loan_votes",
                columns: new[] { "loan_id", "voter_customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ekub_loans_borrower",
                schema: "bank",
                table: "ekub_loans",
                column: "borrower_customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_loans_group",
                schema: "bank",
                table: "ekub_loans",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_loans_status",
                schema: "bank",
                table: "ekub_loans",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ekub_loan_repayments",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "ekub_loan_votes",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "ekub_loans",
                schema: "bank");
        }
    }
}
