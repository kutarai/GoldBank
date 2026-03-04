using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniBank.Migrator.Migrations.UniBankDb
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
                name: "accounts",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone_country_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    pin_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    device_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kyc_level = table.Column<int>(type: "integer", nullable: false),
                    daily_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    monthly_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    available_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    date_of_birth = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    national_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admin_users",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_commissions",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    transaction_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    commission_rate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    commission_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_commissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_floats",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    float_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    float_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_floats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    details = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bill_payments",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bill_providers",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requires_meter_number = table.Column<bool>(type: "boolean", nullable: false),
                    requires_account_number = table.Column<bool>(type: "boolean", nullable: false),
                    min_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    max_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    country_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_transfer_requests",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_device_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    new_device_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_transfer_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "disputes",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    refund_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fraud_alerts",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    admin_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fraud_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fraud_rules",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rule_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    parameters = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fraud_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "kyc_documents",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    encryption_key_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kyc_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "merchant_documents",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    encryption_key_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "merchant_settlements",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_transactions = table.Column<int>(type: "integer", nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_fees = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchant_settlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "merchants",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    owner_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    business_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    registration_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tax_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    category_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    business_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    gps_latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    gps_longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    gps_accuracy_meters = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    is_agent = table.Column<bool>(type: "boolean", nullable: false),
                    agent_terms_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    agent_terms_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kyc_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_tokens",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    card_pan_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    device_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nfc_data = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    qr_code_data = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    terminal_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    device_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "saved_billers",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_billers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_configs",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value_json = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_branding",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    app_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    primary_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    secondary_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    accent_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    favicon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    support_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    support_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    custom_css = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_branding", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_fee_configs",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fee_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    min_fee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    max_fee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_fee_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_transaction_limits",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    per_transaction_limit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    daily_limit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    monthly_limit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_transaction_limits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    counterparty_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    counterparty_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "transfers",
                schema: "bank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    send_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    send_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    receive_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    receive_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    exchange_rate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    estimated_delivery = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_phone_unique",
                schema: "bank",
                table: "accounts",
                column: "phone",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_tenant_id",
                schema: "bank",
                table: "accounts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_email",
                schema: "bank",
                table: "admin_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_username",
                schema: "bank",
                table: "admin_users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_commissions_merchant_id",
                schema: "bank",
                table: "agent_commissions",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_commissions_transaction_id",
                schema: "bank",
                table: "agent_commissions",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_floats_merchant_id",
                schema: "bank",
                table: "agent_floats",
                column: "merchant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_admin_user_id",
                schema: "bank",
                table: "audit_logs",
                column: "admin_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at",
                schema: "bank",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type",
                schema: "bank",
                table: "audit_logs",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "ix_bill_payments_account_id",
                schema: "bank",
                table: "bill_payments",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_payments_provider_id",
                schema: "bank",
                table: "bill_payments",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_bill_payments_reference_unique",
                schema: "bank",
                table: "bill_payments",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bill_providers_category",
                schema: "bank",
                table: "bill_providers",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_bill_providers_code_unique",
                schema: "bank",
                table: "bill_providers",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bill_providers_country_code",
                schema: "bank",
                table: "bill_providers",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_device_transfers_account_id",
                schema: "bank",
                table: "device_transfer_requests",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_transfers_ref",
                schema: "bank",
                table: "device_transfer_requests",
                column: "transfer_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_disputes_account_id",
                schema: "bank",
                table: "disputes",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_status",
                schema: "bank",
                table: "disputes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_disputes_transaction_id",
                schema: "bank",
                table: "disputes",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_alerts_account_id",
                schema: "bank",
                table: "fraud_alerts",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_alerts_severity",
                schema: "bank",
                table: "fraud_alerts",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_alerts_status",
                schema: "bank",
                table: "fraud_alerts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_alerts_tenant_created",
                schema: "bank",
                table: "fraud_alerts",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_fraud_alerts_transaction_id",
                schema: "bank",
                table: "fraud_alerts",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_rules_rule_type",
                schema: "bank",
                table: "fraud_rules",
                column: "rule_type");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_rules_tenant_active",
                schema: "bank",
                table: "fraud_rules",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_fraud_rules_tenant_id",
                schema: "bank",
                table: "fraud_rules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_kyc_documents_account_id",
                schema: "bank",
                table: "kyc_documents",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_kyc_documents_status",
                schema: "bank",
                table: "kyc_documents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_documents_merchant_id",
                schema: "bank",
                table: "merchant_documents",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_settlements_merchant",
                schema: "bank",
                table: "merchant_settlements",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_settlements_period",
                schema: "bank",
                table: "merchant_settlements",
                columns: new[] { "merchant_id", "period_start", "period_end", "currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_merchant_settlements_status",
                schema: "bank",
                table: "merchant_settlements",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_settlements_tenant",
                schema: "bank",
                table: "merchant_settlements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchants_code",
                schema: "bank",
                table: "merchants",
                column: "merchant_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_merchants_name_tenant",
                schema: "bank",
                table: "merchants",
                columns: new[] { "business_name", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_merchants_owner",
                schema: "bank",
                table: "merchants",
                column: "owner_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchants_status",
                schema: "bank",
                table: "merchants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_merchants_tenant",
                schema: "bank",
                table: "merchants",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_tokens_account_device",
                schema: "bank",
                table: "payment_tokens",
                columns: new[] { "account_id", "device_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_tokens_account_id",
                schema: "bank",
                table: "payment_tokens",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_tokens_reference_unique",
                schema: "bank",
                table: "payment_tokens",
                column: "token_reference",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_tokens_token_unique",
                schema: "bank",
                table: "payment_tokens",
                column: "token",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_merchant_account_id",
                schema: "bank",
                table: "payments",
                column: "merchant_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payer_account_id",
                schema: "bank",
                table: "payments",
                column: "payer_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payer_created",
                schema: "bank",
                table: "payments",
                columns: new[] { "payer_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_reference_unique",
                schema: "bank",
                table: "payments",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_status",
                schema: "bank",
                table: "payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_account_id",
                schema: "bank",
                table: "refresh_tokens",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                schema: "bank",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_billers_account_id",
                schema: "bank",
                table: "saved_billers",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_billers_account_provider_ref_unique",
                schema: "bank",
                table: "saved_billers",
                columns: new[] { "account_id", "provider_id", "billing_reference" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_system_configs_key_tenant",
                schema: "bank",
                table: "system_configs",
                columns: new[] { "key", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_branding_tenant",
                schema: "bank",
                table: "tenant_branding",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_fee_configs_tenant_type",
                schema: "bank",
                table: "tenant_fee_configs",
                columns: new[] { "tenant_id", "transaction_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_transaction_limits_tenant_type",
                schema: "bank",
                table: "tenant_transaction_limits",
                columns: new[] { "tenant_id", "transaction_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_account_created",
                schema: "bank",
                table: "transactions",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_account_id",
                schema: "bank",
                table: "transactions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_created_at",
                schema: "bank",
                table: "transactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_transfers_recipient_account_id",
                schema: "bank",
                table: "transfers",
                column: "recipient_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfers_reference_unique",
                schema: "bank",
                table: "transfers",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transfers_sender_account_id",
                schema: "bank",
                table: "transfers",
                column: "sender_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_transfers_sender_created",
                schema: "bank",
                table: "transfers",
                columns: new[] { "sender_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transfers_status",
                schema: "bank",
                table: "transfers",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "admin_users",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "agent_commissions",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "agent_floats",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "bill_payments",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "bill_providers",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "device_transfer_requests",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "disputes",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "fraud_alerts",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "fraud_rules",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "kyc_documents",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "merchant_documents",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "merchant_settlements",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "merchants",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "payment_tokens",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "saved_billers",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "system_configs",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "tenant_branding",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "tenant_fee_configs",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "tenant_transaction_limits",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "transactions",
                schema: "bank");

            migrationBuilder.DropTable(
                name: "transfers",
                schema: "bank");
        }
    }
}
