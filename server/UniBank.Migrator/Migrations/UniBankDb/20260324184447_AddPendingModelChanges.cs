using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniBank.Migrator.Migrations.UniBankDb
{
    /// <inheritdoc />
    public partial class AddPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Tax",
                schema: "bank",
                table: "transactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MerchantCommission",
                schema: "bank",
                table: "payments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Tax",
                schema: "bank",
                table: "payments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "bank",
                table: "admin_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_interactions",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    request_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    response_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    model_used = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    inference_time_ms = table.Column<int>(type: "integer", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_interactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                schema: "bank",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_prices",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    price_per_gram_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    price_per_oz_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_prices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "deposit_houses",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    license_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    api_endpoint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    trust_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deposit_houses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "kyc_verifications",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selfie_image_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    id_document_image_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    face_match_score = table.Column<double>(type: "double precision", nullable: false),
                    face_match_decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    extracted_full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    extracted_id_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    extracted_date_of_birth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    extracted_nationality = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    extracted_gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    name_match = table.Column<bool>(type: "boolean", nullable: true),
                    id_number_match = table.Column<bool>(type: "boolean", nullable: true),
                    dob_match = table.Column<bool>(type: "boolean", nullable: true),
                    overall_decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kyc_verifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "transaction_disputes",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    evidence_image_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dispute_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ai_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ai_recommended_action = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    classification_confidence = table.Column<double>(type: "double precision", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_team = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    resolution_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_disputes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    asset_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight_grams = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    purity = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: true),
                    receipt_image_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    receipt_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_valuation_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    last_valuation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_verification_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verification_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assets_deposit_houses_deposit_house_id",
                        column: x => x.deposit_house_id,
                        principalSchema: "bank",
                        principalTable: "deposit_houses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_valuations",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valuation_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    valuer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valuer_license = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    report_image_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_valuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_valuations_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "bank",
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_interactions_account_id",
                schema: "bank",
                table: "ai_interactions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_interactions_created",
                schema: "bank",
                table: "ai_interactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_interactions_type",
                schema: "bank",
                table: "ai_interactions",
                column: "interaction_type");

            migrationBuilder.CreateIndex(
                name: "ix_asset_valuations_asset_created",
                schema: "bank",
                table: "asset_valuations",
                columns: new[] { "asset_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_valuations_asset_id",
                schema: "bank",
                table: "asset_valuations",
                column: "asset_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_assets_deposit_house_id",
                schema: "bank",
                table: "assets",
                column: "deposit_house_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_deposit_house_receipt_unique",
                schema: "bank",
                table: "assets",
                columns: new[] { "deposit_house_id", "receipt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assets_is_deleted",
                schema: "bank",
                table: "assets",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_assets_status",
                schema: "bank",
                table: "assets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_branches_code_tenant_id",
                schema: "bank",
                table: "branches",
                columns: new[] { "code", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_daily_prices_asset_type_date_unique",
                schema: "bank",
                table: "daily_prices",
                columns: new[] { "asset_type", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_daily_prices_date",
                schema: "bank",
                table: "daily_prices",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_deposit_houses_is_active",
                schema: "bank",
                table: "deposit_houses",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_deposit_houses_tenant_id",
                schema: "bank",
                table: "deposit_houses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_deposit_houses_tenant_license_unique",
                schema: "bank",
                table: "deposit_houses",
                columns: new[] { "tenant_id", "license_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kyc_verifications_account_created",
                schema: "bank",
                table: "kyc_verifications",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_kyc_verifications_account_id",
                schema: "bank",
                table: "kyc_verifications",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_kyc_verifications_decision",
                schema: "bank",
                table: "kyc_verifications",
                column: "overall_decision");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_disputes_account_created",
                schema: "bank",
                table: "transaction_disputes",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_disputes_account_id",
                schema: "bank",
                table: "transaction_disputes",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_disputes_reference_unique",
                schema: "bank",
                table: "transaction_disputes",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transaction_disputes_status",
                schema: "bank",
                table: "transaction_disputes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_disputes_transaction_id",
                schema: "bank",
                table: "transaction_disputes",
                column: "transaction_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_interactions",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "asset_valuations",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "branches",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "daily_prices",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "kyc_verifications",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "transaction_disputes",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "deposit_houses",
                schema: "bank");

            migrationBuilder.DropColumn(
                name: "Tax",
                schema: "bank",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "MerchantCommission",
                schema: "bank",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "Tax",
                schema: "bank",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "bank",
                table: "admin_users");
        }
    }
}
