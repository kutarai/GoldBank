using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    public partial class AddEkubModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ekub_contributions",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    confirmed_by_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ekub_contributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ekub_fees",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ekub_fees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ekub_groups",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    monthly_contribution = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    loan_interest_rate_percent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    chairman_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_fee_applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ekub_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ekub_invitations",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitee_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    invitee_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inviter_customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ekub_invitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ekub_memberships",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exit_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ekub_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ekub_memberships_ekub_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "bank",
                        principalTable: "ekub_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ekub_contributions_customer_status",
                schema: "bank",
                table: "ekub_contributions",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ekub_contributions_group_period",
                schema: "bank",
                table: "ekub_contributions",
                columns: new[] { "group_id", "period" });

            migrationBuilder.CreateIndex(
                name: "ix_ekub_contributions_membership",
                schema: "bank",
                table: "ekub_contributions",
                column: "membership_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_fees_group_period_unique",
                schema: "bank",
                table: "ekub_fees",
                columns: new[] { "group_id", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ekub_groups_chairman",
                schema: "bank",
                table: "ekub_groups",
                column: "chairman_customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_groups_status",
                schema: "bank",
                table: "ekub_groups",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_groups_tenant_id",
                schema: "bank",
                table: "ekub_groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_invitations_group_phone_pending_unique",
                schema: "bank",
                table: "ekub_invitations",
                columns: new[] { "group_id", "invitee_phone" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_invitations_invitee_customer",
                schema: "bank",
                table: "ekub_invitations",
                column: "invitee_customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_invitations_invitee_phone",
                schema: "bank",
                table: "ekub_invitations",
                column: "invitee_phone");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_memberships_customer",
                schema: "bank",
                table: "ekub_memberships",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_memberships_group",
                schema: "bank",
                table: "ekub_memberships",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_ekub_memberships_group_customer_active_unique",
                schema: "bank",
                table: "ekub_memberships",
                columns: new[] { "group_id", "customer_id" },
                unique: true,
                filter: "left_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ekub_contributions",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "ekub_fees",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "ekub_invitations",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "ekub_memberships",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "ekub_groups",
                schema: "bank");
        }
    }
}
