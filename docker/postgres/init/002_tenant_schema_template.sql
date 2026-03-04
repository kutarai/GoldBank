-- ============================================================================
-- UniBank Multi-Tenant Database Schema
-- 002_tenant_schema_template.sql - Tenant schema provisioning function
-- ============================================================================

-- Monthly partition creation function
CREATE OR REPLACE FUNCTION public.create_monthly_partitions(
    p_schema_name TEXT,
    p_table_name TEXT,
    p_months_ahead INT DEFAULT 3
) RETURNS VOID AS $$
DECLARE
    partition_date DATE;
    partition_name TEXT;
    start_date DATE;
    end_date DATE;
BEGIN
    FOR i IN 0..p_months_ahead LOOP
        partition_date := DATE_TRUNC('month', NOW()) + (i || ' months')::INTERVAL;
        partition_name := p_table_name || '_' || TO_CHAR(partition_date, 'YYYY_MM');
        start_date := partition_date;
        end_date := partition_date + '1 month'::INTERVAL;

        IF NOT EXISTS (
            SELECT 1 FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = p_schema_name
            AND c.relname = partition_name
        ) THEN
            EXECUTE FORMAT(
                'CREATE TABLE %I.%I PARTITION OF %I.%I FOR VALUES FROM (%L) TO (%L)',
                p_schema_name, partition_name,
                p_schema_name, p_table_name,
                start_date, end_date
            );
        END IF;
    END LOOP;
END;
$$ LANGUAGE plpgsql;

-- Function to provision a new tenant schema with all tables
CREATE OR REPLACE FUNCTION public.provision_tenant_schema(
    p_schema_name TEXT
) RETURNS VOID AS $$
BEGIN
    -- Create schema
    EXECUTE FORMAT('CREATE SCHEMA IF NOT EXISTS %I', p_schema_name);

    -- accounts table
    EXECUTE FORMAT('
        CREATE TABLE %I.accounts (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            phone VARCHAR(20) NOT NULL,
            phone_country_code VARCHAR(5) NOT NULL DEFAULT ''+27'',
            first_name VARCHAR(100),
            last_name VARCHAR(100),
            date_of_birth DATE,
            national_id VARCHAR(50),
            email VARCHAR(255),
            pin_hash VARCHAR(255),
            status VARCHAR(30) NOT NULL DEFAULT ''pending_kyc''
                CHECK (status IN (''pending_kyc'', ''active'', ''suspended'', ''closed'', ''frozen'')),
            kyc_level INT NOT NULL DEFAULT 0,
            daily_limit DECIMAL(18,2) NOT NULL DEFAULT 1000.00,
            monthly_limit DECIMAL(18,2) NOT NULL DEFAULT 5000.00,
            balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
            available_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
            currency VARCHAR(3) NOT NULL DEFAULT ''ZAR'',
            device_id VARCHAR(255),
            fcm_token TEXT,
            last_login_at TIMESTAMPTZ,
            failed_pin_attempts INT NOT NULL DEFAULT 0,
            pin_locked_until TIMESTAMPTZ,
            tenant_id UUID NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            deleted_at TIMESTAMPTZ,
            UNIQUE(phone)
        )', p_schema_name);

    -- transactions table (partitioned by month)
    EXECUTE FORMAT('
        CREATE TABLE %I.transactions (
            id UUID NOT NULL DEFAULT gen_random_uuid(),
            account_id UUID NOT NULL,
            type VARCHAR(30) NOT NULL
                CHECK (type IN (''cash_in'', ''cash_out'', ''p2p_send'', ''p2p_receive'',
                                ''payment_nfc'', ''payment_qr'', ''bill_payment'',
                                ''transfer_domestic'', ''transfer_cross_border'',
                                ''fee'', ''reversal'', ''settlement'')),
            amount DECIMAL(18,2) NOT NULL,
            fee DECIMAL(18,2) NOT NULL DEFAULT 0.00,
            currency VARCHAR(3) NOT NULL DEFAULT ''ZAR'',
            status VARCHAR(20) NOT NULL DEFAULT ''pending''
                CHECK (status IN (''pending'', ''processing'', ''completed'', ''failed'', ''reversed'')),
            reference VARCHAR(50) NOT NULL UNIQUE,
            counterparty_account_id UUID,
            counterparty_phone VARCHAR(20),
            counterparty_name VARCHAR(200),
            merchant_id UUID,
            agent_id UUID,
            terminal_id UUID,
            description TEXT,
            metadata JSONB DEFAULT ''{}'',
            balance_before DECIMAL(18,2),
            balance_after DECIMAL(18,2),
            failure_reason TEXT,
            completed_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            PRIMARY KEY (id, created_at)
        ) PARTITION BY RANGE (created_at)', p_schema_name);

    -- kyc_documents
    EXECUTE FORMAT('
        CREATE TABLE %I.kyc_documents (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            account_id UUID NOT NULL,
            document_type VARCHAR(30) NOT NULL
                CHECK (document_type IN (''national_id'', ''passport'', ''drivers_license'',
                                          ''proof_of_address'', ''selfie'', ''utility_bill'')),
            document_number VARCHAR(100),
            file_path TEXT NOT NULL,
            file_hash VARCHAR(128) NOT NULL,
            status VARCHAR(20) NOT NULL DEFAULT ''pending''
                CHECK (status IN (''pending'', ''approved'', ''rejected'', ''expired'')),
            reviewed_by UUID,
            review_notes TEXT,
            reviewed_at TIMESTAMPTZ,
            expires_at DATE,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        )', p_schema_name);

    -- merchants
    EXECUTE FORMAT('
        CREATE TABLE %I.merchants (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            account_id UUID NOT NULL,
            business_name VARCHAR(200) NOT NULL,
            business_type VARCHAR(50) NOT NULL,
            registration_number VARCHAR(100),
            tax_id VARCHAR(50),
            category_code VARCHAR(10) NOT NULL,
            address_line1 VARCHAR(255),
            address_line2 VARCHAR(255),
            city VARCHAR(100),
            province VARCHAR(100),
            postal_code VARCHAR(20),
            country_code VARCHAR(3) NOT NULL DEFAULT ''ZAF'',
            settlement_account_id UUID,
            settlement_frequency VARCHAR(20) NOT NULL DEFAULT ''daily''
                CHECK (settlement_frequency IN (''daily'', ''weekly'', ''monthly'')),
            commission_rate DECIMAL(5,4) NOT NULL DEFAULT 0.0150,
            status VARCHAR(20) NOT NULL DEFAULT ''pending''
                CHECK (status IN (''pending'', ''active'', ''suspended'', ''closed'')),
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            deleted_at TIMESTAMPTZ
        )', p_schema_name);

    -- agents
    EXECUTE FORMAT('
        CREATE TABLE %I.agents (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            account_id UUID NOT NULL,
            agent_code VARCHAR(20) NOT NULL UNIQUE,
            business_name VARCHAR(200) NOT NULL,
            location_lat DECIMAL(10,8),
            location_lng DECIMAL(11,8),
            address TEXT,
            city VARCHAR(100),
            province VARCHAR(100),
            float_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
            float_limit DECIMAL(18,2) NOT NULL DEFAULT 50000.00,
            commission_rate DECIMAL(5,4) NOT NULL DEFAULT 0.0100,
            status VARCHAR(20) NOT NULL DEFAULT ''pending''
                CHECK (status IN (''pending'', ''active'', ''suspended'', ''closed'')),
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            deleted_at TIMESTAMPTZ
        )', p_schema_name);

    -- terminals
    EXECUTE FORMAT('
        CREATE TABLE %I.terminals (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            merchant_id UUID NOT NULL,
            serial_number VARCHAR(100) NOT NULL UNIQUE,
            model VARCHAR(100) NOT NULL,
            firmware_version VARCHAR(50),
            status VARCHAR(20) NOT NULL DEFAULT ''inactive''
                CHECK (status IN (''inactive'', ''active'', ''offline'', ''decommissioned'')),
            last_heartbeat_at TIMESTAMPTZ,
            last_key_injection_at TIMESTAMPTZ,
            ip_address INET,
            location_description TEXT,
            config_json JSONB DEFAULT ''{}'',
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        )', p_schema_name);

    -- audit_logs (partitioned by month)
    EXECUTE FORMAT('
        CREATE TABLE %I.audit_logs (
            id UUID NOT NULL DEFAULT gen_random_uuid(),
            entity_type VARCHAR(50) NOT NULL,
            entity_id UUID NOT NULL,
            action VARCHAR(50) NOT NULL,
            actor_id UUID,
            actor_type VARCHAR(20) NOT NULL DEFAULT ''user''
                CHECK (actor_type IN (''user'', ''admin'', ''system'', ''agent'')),
            changes JSONB,
            ip_address INET,
            user_agent TEXT,
            timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            PRIMARY KEY (id, timestamp)
        ) PARTITION BY RANGE (timestamp)', p_schema_name);

    -- notifications
    EXECUTE FORMAT('
        CREATE TABLE %I.notifications (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            account_id UUID NOT NULL,
            type VARCHAR(30) NOT NULL
                CHECK (type IN (''transaction'', ''security'', ''kyc'', ''marketing'', ''system'')),
            channel VARCHAR(20) NOT NULL
                CHECK (channel IN (''push'', ''sms'', ''email'', ''in_app'')),
            title VARCHAR(200),
            body TEXT NOT NULL,
            status VARCHAR(20) NOT NULL DEFAULT ''pending''
                CHECK (status IN (''pending'', ''sent'', ''delivered'', ''failed'', ''read'')),
            sent_at TIMESTAMPTZ,
            delivered_at TIMESTAMPTZ,
            read_at TIMESTAMPTZ,
            metadata JSONB DEFAULT ''{}'',
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        )', p_schema_name);

    -- disputes
    EXECUTE FORMAT('
        CREATE TABLE %I.disputes (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            transaction_id UUID NOT NULL,
            account_id UUID NOT NULL,
            type VARCHAR(30) NOT NULL
                CHECK (type IN (''unauthorized'', ''incorrect_amount'', ''service_not_received'',
                                ''duplicate'', ''other'')),
            description TEXT NOT NULL,
            status VARCHAR(20) NOT NULL DEFAULT ''open''
                CHECK (status IN (''open'', ''investigating'', ''resolved'', ''rejected'', ''escalated'')),
            resolution TEXT,
            resolved_by UUID,
            resolved_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        )', p_schema_name);

    -- reconciliation
    EXECUTE FORMAT('
        CREATE TABLE %I.reconciliation (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            batch_date DATE NOT NULL,
            partner_code VARCHAR(50) NOT NULL,
            total_transactions INT NOT NULL DEFAULT 0,
            total_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
            matched_count INT NOT NULL DEFAULT 0,
            unmatched_count INT NOT NULL DEFAULT 0,
            status VARCHAR(20) NOT NULL DEFAULT ''pending''
                CHECK (status IN (''pending'', ''in_progress'', ''completed'', ''discrepancy'')),
            discrepancy_details JSONB,
            completed_at TIMESTAMPTZ,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            UNIQUE(batch_date, partner_code)
        )', p_schema_name);

    -- Create indexes
    EXECUTE FORMAT('CREATE INDEX idx_%s_accounts_phone ON %I.accounts (phone)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_accounts_status ON %I.accounts (status)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_accounts_national_id ON %I.accounts (national_id) WHERE national_id IS NOT NULL', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_kyc_account_id ON %I.kyc_documents (account_id)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_kyc_status ON %I.kyc_documents (status) WHERE status = ''pending''', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_merchants_account_id ON %I.merchants (account_id)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_merchants_category ON %I.merchants (category_code)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_agents_account_id ON %I.agents (account_id)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_terminals_merchant_id ON %I.terminals (merchant_id)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_terminals_serial ON %I.terminals (serial_number)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_notifications_account ON %I.notifications (account_id, created_at DESC)', replace(p_schema_name, 'tenant_', ''), p_schema_name);
    EXECUTE FORMAT('CREATE INDEX idx_%s_notifications_status ON %I.notifications (status) WHERE status = ''pending''', replace(p_schema_name, 'tenant_', ''), p_schema_name);

    -- Create initial transaction partitions (current month + 3 ahead)
    PERFORM public.create_monthly_partitions(p_schema_name, 'transactions', 3);
    PERFORM public.create_monthly_partitions(p_schema_name, 'audit_logs', 3);
END;
$$ LANGUAGE plpgsql;
