# STORY-003: PostgreSQL Database Schema & Multi-Tenant Foundation

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a developer,
I want the database schema created with multi-tenant support,
So that all services can persist data with tenant isolation.

---

## Description

### Background
GoldBank is a white-label platform where each deployment (tenant) serves a different banking brand. Data isolation between tenants is critical for security, compliance, and regulatory requirements across different Southern African jurisdictions. The schema-per-tenant approach provides strong isolation -- each tenant gets its own PostgreSQL schema with identical table structures, while shared/global data (tenant registry, bill providers, system configuration) lives in a `public` schema.

This story establishes the database foundation including the multi-tenant resolution mechanism, EF Core DbContext configuration, migration framework, and table partitioning strategy for high-volume tables.

### Scope

**In scope:**
- Public schema tables: `tenants`, `bill_providers`, `system_config`
- Tenant schema template with all domain tables
- `ITenantProvider` interface and implementation that resolves tenant from gRPC metadata
- EF Core `TenantDbContext` with dynamic schema switching
- EF Core `PublicDbContext` for shared data
- Migration framework supporting both public and tenant schemas
- Monthly partitioning on `transactions` and `audit_logs` tables
- Automated partition creation mechanism
- Seed data for default tenant and system configuration
- Database indexing strategy for common query patterns

**Out of scope:**
- Cross-tenant data queries (admin reporting will use a separate read path)
- Database replication or read replicas
- Connection pooling configuration (PgBouncer) -- separate infrastructure story
- Data encryption at rest (PostgreSQL TDE)

### User Flow
1. Application starts and connects to PostgreSQL
2. Public schema migrations run automatically on startup
3. When a new tenant is provisioned, the system:
   a. Creates a new PostgreSQL schema named `tenant_{tenant_code}`
   b. Runs all tenant schema migrations against the new schema
   c. Inserts tenant record into `public.tenants`
4. For each incoming gRPC request:
   a. `ITenantProvider` extracts `tenant_id` from gRPC metadata/JWT claims
   b. `TenantDbContext` sets `search_path` to the resolved tenant schema
   c. All queries execute against the tenant-specific schema
5. Tenant data is completely isolated; queries never cross schema boundaries

---

## Acceptance Criteria

- [ ] Public schema contains `tenants`, `bill_providers`, and `system_config` tables with correct columns and constraints
- [ ] Tenant schema template contains all domain tables: `accounts`, `transactions`, `kyc_documents`, `merchants`, `agents`, `terminals`, `audit_logs`, `notifications`, `disputes`, `reconciliation`
- [ ] `ITenantProvider` interface is defined in `GoldBank.SharedKernel` with implementation in `GoldBank.Core`
- [ ] `ITenantProvider` resolves tenant schema from gRPC metadata (`x-tenant-id` header or JWT `tenant_id` claim)
- [ ] `TenantDbContext` dynamically sets PostgreSQL `search_path` based on resolved tenant
- [ ] `PublicDbContext` always operates on the `public` schema
- [ ] EF Core migrations can be applied to both public and tenant schemas
- [ ] New tenant provisioning creates schema and runs migrations automatically
- [ ] `transactions` table is partitioned by month on `created_at` column
- [ ] `audit_logs` table is partitioned by month on `timestamp` column
- [ ] Automated partition creation generates partitions 3 months ahead
- [ ] Seed data creates a default tenant (`goldbank_default`) for development
- [ ] All tables have appropriate indexes for expected query patterns
- [ ] All tables include `created_at` and `updated_at` audit columns
- [ ] Soft delete is supported via `deleted_at` nullable timestamp column where applicable

---

## Technical Notes

### Components

**Affected Projects:**
- `GoldBank.SharedKernel` -- `ITenantProvider`, `TenantInfo` types
- `GoldBank.Core` -- `TenantDbContext`, `PublicDbContext`, migration framework, tenant provider implementation
- `GoldBank.Core.Modules.Accounts.Infrastructure.Persistence` -- account-related entity configurations

### Public Schema Tables

```sql
-- public.tenants
CREATE TABLE public.tenants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,
    schema_name VARCHAR(63) NOT NULL UNIQUE,
    config_json JSONB NOT NULL DEFAULT '{}',
    branding_json JSONB NOT NULL DEFAULT '{}',
    status VARCHAR(20) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'suspended', 'decommissioned')),
    max_users INT NOT NULL DEFAULT 1000000,
    country_code VARCHAR(3) NOT NULL DEFAULT 'ZAF',
    currency_code VARCHAR(3) NOT NULL DEFAULT 'ZAR',
    timezone VARCHAR(50) NOT NULL DEFAULT 'Africa/Johannesburg',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- public.bill_providers
CREATE TABLE public.bill_providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,
    category VARCHAR(50) NOT NULL, -- electricity, water, airtime, dstv, etc.
    config JSONB NOT NULL DEFAULT '{}', -- API endpoint, credentials reference, etc.
    countries TEXT[] NOT NULL DEFAULT '{ZAF}',
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- public.system_config
CREATE TABLE public.system_config (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key VARCHAR(200) NOT NULL,
    value JSONB NOT NULL,
    tenant_id UUID REFERENCES public.tenants(id),
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(key, tenant_id)
);
-- tenant_id NULL = global config, non-null = tenant-specific override
```

### Tenant Schema Tables

```sql
-- {tenant_schema}.accounts
CREATE TABLE {schema}.accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    phone VARCHAR(20) NOT NULL UNIQUE,
    phone_country_code VARCHAR(5) NOT NULL DEFAULT '+27',
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    date_of_birth DATE,
    national_id VARCHAR(50),
    email VARCHAR(255),
    pin_hash VARCHAR(255),
    status VARCHAR(30) NOT NULL DEFAULT 'pending_kyc'
        CHECK (status IN ('pending_kyc', 'active', 'suspended', 'closed', 'frozen')),
    kyc_level INT NOT NULL DEFAULT 0, -- 0=none, 1=basic, 2=enhanced, 3=full
    daily_limit DECIMAL(18,2) NOT NULL DEFAULT 1000.00,
    monthly_limit DECIMAL(18,2) NOT NULL DEFAULT 5000.00,
    balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    available_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    currency VARCHAR(3) NOT NULL DEFAULT 'ZAR',
    device_id VARCHAR(255),
    fcm_token TEXT,
    last_login_at TIMESTAMPTZ,
    failed_pin_attempts INT NOT NULL DEFAULT 0,
    pin_locked_until TIMESTAMPTZ,
    tenant_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMPTZ
);

-- {tenant_schema}.transactions (partitioned)
CREATE TABLE {schema}.transactions (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES {schema}.accounts(id),
    type VARCHAR(30) NOT NULL
        CHECK (type IN ('cash_in', 'cash_out', 'p2p_send', 'p2p_receive',
                        'payment_nfc', 'payment_qr', 'bill_payment',
                        'transfer_domestic', 'transfer_cross_border',
                        'fee', 'reversal', 'settlement')),
    amount DECIMAL(18,2) NOT NULL,
    fee DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    currency VARCHAR(3) NOT NULL DEFAULT 'ZAR',
    status VARCHAR(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'reversed')),
    reference VARCHAR(50) NOT NULL UNIQUE,
    counterparty_account_id UUID,
    counterparty_phone VARCHAR(20),
    counterparty_name VARCHAR(200),
    merchant_id UUID,
    agent_id UUID,
    terminal_id UUID,
    description TEXT,
    metadata JSONB DEFAULT '{}',
    balance_before DECIMAL(18,2),
    balance_after DECIMAL(18,2),
    failure_reason TEXT,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

-- {tenant_schema}.kyc_documents
CREATE TABLE {schema}.kyc_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES {schema}.accounts(id),
    document_type VARCHAR(30) NOT NULL
        CHECK (document_type IN ('national_id', 'passport', 'drivers_license',
                                  'proof_of_address', 'selfie', 'utility_bill')),
    document_number VARCHAR(100),
    file_path TEXT NOT NULL,
    file_hash VARCHAR(128) NOT NULL, -- SHA-512 hash of uploaded file
    status VARCHAR(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'approved', 'rejected', 'expired')),
    reviewed_by UUID,
    review_notes TEXT,
    reviewed_at TIMESTAMPTZ,
    expires_at DATE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- {tenant_schema}.merchants
CREATE TABLE {schema}.merchants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES {schema}.accounts(id),
    business_name VARCHAR(200) NOT NULL,
    business_type VARCHAR(50) NOT NULL,
    registration_number VARCHAR(100),
    tax_id VARCHAR(50),
    category_code VARCHAR(10) NOT NULL, -- MCC code
    address_line1 VARCHAR(255),
    address_line2 VARCHAR(255),
    city VARCHAR(100),
    province VARCHAR(100),
    postal_code VARCHAR(20),
    country_code VARCHAR(3) NOT NULL DEFAULT 'ZAF',
    settlement_account_id UUID REFERENCES {schema}.accounts(id),
    settlement_frequency VARCHAR(20) NOT NULL DEFAULT 'daily'
        CHECK (settlement_frequency IN ('daily', 'weekly', 'monthly')),
    commission_rate DECIMAL(5,4) NOT NULL DEFAULT 0.0150, -- 1.5%
    status VARCHAR(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'active', 'suspended', 'closed')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMPTZ
);

-- {tenant_schema}.agents
CREATE TABLE {schema}.agents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES {schema}.accounts(id),
    agent_code VARCHAR(20) NOT NULL UNIQUE,
    business_name VARCHAR(200) NOT NULL,
    location_lat DECIMAL(10,8),
    location_lng DECIMAL(11,8),
    address TEXT,
    city VARCHAR(100),
    province VARCHAR(100),
    float_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    float_limit DECIMAL(18,2) NOT NULL DEFAULT 50000.00,
    commission_rate DECIMAL(5,4) NOT NULL DEFAULT 0.0100, -- 1.0%
    status VARCHAR(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'active', 'suspended', 'closed')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMPTZ
);

-- {tenant_schema}.terminals
CREATE TABLE {schema}.terminals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id UUID NOT NULL REFERENCES {schema}.merchants(id),
    serial_number VARCHAR(100) NOT NULL UNIQUE,
    model VARCHAR(100) NOT NULL,
    firmware_version VARCHAR(50),
    status VARCHAR(20) NOT NULL DEFAULT 'inactive'
        CHECK (status IN ('inactive', 'active', 'offline', 'decommissioned')),
    last_heartbeat_at TIMESTAMPTZ,
    last_key_injection_at TIMESTAMPTZ,
    ip_address INET,
    location_description TEXT,
    config_json JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- {tenant_schema}.audit_logs (partitioned)
CREATE TABLE {schema}.audit_logs (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    entity_type VARCHAR(50) NOT NULL,
    entity_id UUID NOT NULL,
    action VARCHAR(50) NOT NULL, -- create, update, delete, login, etc.
    actor_id UUID,
    actor_type VARCHAR(20) NOT NULL DEFAULT 'user'
        CHECK (actor_type IN ('user', 'admin', 'system', 'agent')),
    changes JSONB,
    ip_address INET,
    user_agent TEXT,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, timestamp)
) PARTITION BY RANGE (timestamp);

-- {tenant_schema}.notifications
CREATE TABLE {schema}.notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES {schema}.accounts(id),
    type VARCHAR(30) NOT NULL
        CHECK (type IN ('transaction', 'security', 'kyc', 'marketing', 'system')),
    channel VARCHAR(20) NOT NULL
        CHECK (channel IN ('push', 'sms', 'email', 'in_app')),
    title VARCHAR(200),
    body TEXT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'sent', 'delivered', 'failed', 'read')),
    sent_at TIMESTAMPTZ,
    delivered_at TIMESTAMPTZ,
    read_at TIMESTAMPTZ,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- {tenant_schema}.disputes
CREATE TABLE {schema}.disputes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id UUID NOT NULL,
    account_id UUID NOT NULL REFERENCES {schema}.accounts(id),
    type VARCHAR(30) NOT NULL
        CHECK (type IN ('unauthorized', 'incorrect_amount', 'service_not_received',
                        'duplicate', 'other')),
    description TEXT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'open'
        CHECK (status IN ('open', 'investigating', 'resolved', 'rejected', 'escalated')),
    resolution TEXT,
    resolved_by UUID,
    resolved_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- {tenant_schema}.reconciliation
CREATE TABLE {schema}.reconciliation (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_date DATE NOT NULL,
    partner_code VARCHAR(50) NOT NULL, -- switching partner identifier
    total_transactions INT NOT NULL DEFAULT 0,
    total_amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    matched_count INT NOT NULL DEFAULT 0,
    unmatched_count INT NOT NULL DEFAULT 0,
    status VARCHAR(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'in_progress', 'completed', 'discrepancy')),
    discrepancy_details JSONB,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(batch_date, partner_code)
);
```

### Indexing Strategy

```sql
-- accounts
CREATE INDEX idx_accounts_phone ON {schema}.accounts (phone);
CREATE INDEX idx_accounts_status ON {schema}.accounts (status);
CREATE INDEX idx_accounts_national_id ON {schema}.accounts (national_id) WHERE national_id IS NOT NULL;

-- transactions
CREATE INDEX idx_transactions_account_id ON {schema}.transactions (account_id, created_at DESC);
CREATE INDEX idx_transactions_reference ON {schema}.transactions (reference);
CREATE INDEX idx_transactions_status ON {schema}.transactions (status) WHERE status IN ('pending', 'processing');
CREATE INDEX idx_transactions_merchant_id ON {schema}.transactions (merchant_id) WHERE merchant_id IS NOT NULL;

-- kyc_documents
CREATE INDEX idx_kyc_account_id ON {schema}.kyc_documents (account_id);
CREATE INDEX idx_kyc_status ON {schema}.kyc_documents (status) WHERE status = 'pending';

-- merchants
CREATE INDEX idx_merchants_account_id ON {schema}.merchants (account_id);
CREATE INDEX idx_merchants_category ON {schema}.merchants (category_code);

-- agents
CREATE INDEX idx_agents_account_id ON {schema}.agents (account_id);
CREATE INDEX idx_agents_location ON {schema}.agents USING GIST (
    ST_MakePoint(location_lng, location_lat)
) WHERE location_lat IS NOT NULL;

-- terminals
CREATE INDEX idx_terminals_merchant_id ON {schema}.terminals (merchant_id);
CREATE INDEX idx_terminals_serial ON {schema}.terminals (serial_number);
CREATE INDEX idx_terminals_status ON {schema}.terminals (status);

-- audit_logs
CREATE INDEX idx_audit_entity ON {schema}.audit_logs (entity_type, entity_id, timestamp DESC);
CREATE INDEX idx_audit_actor ON {schema}.audit_logs (actor_id, timestamp DESC);

-- notifications
CREATE INDEX idx_notifications_account ON {schema}.notifications (account_id, created_at DESC);
CREATE INDEX idx_notifications_status ON {schema}.notifications (status) WHERE status = 'pending';
```

### Multi-Tenant Implementation

**ITenantProvider Interface (SharedKernel):**
```csharp
public interface ITenantProvider
{
    TenantInfo GetCurrentTenant();
    Task<TenantInfo> GetTenantByIdAsync(Guid tenantId);
    Task<TenantInfo> GetTenantByCodeAsync(string code);
}

public record TenantInfo(
    Guid Id,
    string Name,
    string Code,
    string SchemaName,
    string CountryCode,
    string CurrencyCode,
    string Timezone,
    string Status
);
```

**TenantDbContext (Core):**
```csharp
public class TenantDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;
    private readonly string _schema;

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
        var tenant = _tenantProvider.GetCurrentTenant();
        _schema = tenant.SchemaName; // e.g., "tenant_goldbank_default"
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);
        // Apply entity configurations...
    }
}
```

**Tenant Resolution from gRPC Metadata:**
```csharp
public class GrpcTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PublicDbContext _publicDb;

    public TenantInfo GetCurrentTenant()
    {
        // Extract from JWT claim or gRPC metadata header
        var httpContext = _httpContextAccessor.HttpContext;
        var tenantId = httpContext?.User?.FindFirst("tenant_id")?.Value
            ?? httpContext?.Request.Headers["x-tenant-id"].FirstOrDefault();

        // Resolve tenant info (cached in Redis)
        // ...
    }
}
```

### Monthly Partitioning Strategy

```sql
-- Create initial partitions for transactions
CREATE TABLE {schema}.transactions_2026_01
    PARTITION OF {schema}.transactions
    FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');

CREATE TABLE {schema}.transactions_2026_02
    PARTITION OF {schema}.transactions
    FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');

-- Automated partition creation function
CREATE OR REPLACE FUNCTION create_monthly_partitions(
    schema_name TEXT,
    table_name TEXT,
    months_ahead INT DEFAULT 3
) RETURNS VOID AS $$
DECLARE
    partition_date DATE;
    partition_name TEXT;
    start_date DATE;
    end_date DATE;
BEGIN
    FOR i IN 0..months_ahead LOOP
        partition_date := DATE_TRUNC('month', NOW()) + (i || ' months')::INTERVAL;
        partition_name := schema_name || '.' || table_name || '_' ||
                         TO_CHAR(partition_date, 'YYYY_MM');
        start_date := partition_date;
        end_date := partition_date + '1 month'::INTERVAL;

        IF NOT EXISTS (
            SELECT 1 FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = schema_name
            AND c.relname = table_name || '_' || TO_CHAR(partition_date, 'YYYY_MM')
        ) THEN
            EXECUTE FORMAT(
                'CREATE TABLE %I.%I PARTITION OF %I.%I FOR VALUES FROM (%L) TO (%L)',
                schema_name,
                table_name || '_' || TO_CHAR(partition_date, 'YYYY_MM'),
                schema_name,
                table_name,
                start_date,
                end_date
            );
        END IF;
    END LOOP;
END;
$$ LANGUAGE plpgsql;
```

### API / gRPC Endpoints
No new gRPC endpoints. This story provides the data persistence layer consumed by service implementations.

### Database Changes
This story IS the database change -- all tables defined above.

### Security Considerations
- Schema-per-tenant provides strong data isolation; a misconfigured `search_path` is the primary risk
- Always validate tenant resolution -- never fall back to a default tenant for data operations
- `pin_hash` column uses bcrypt; ensure no raw PINs are ever logged or stored
- Audit logs capture all state changes for compliance
- Use parameterized queries exclusively (EF Core handles this, but be cautious with raw SQL for partitioning)
- Database user for application should not have `CREATE SCHEMA` privilege -- use a separate admin connection for tenant provisioning
- `config_json` in tenants table may contain sensitive configuration -- encrypt at application level

### Edge Cases
- Concurrent tenant provisioning: Use advisory locks to prevent race conditions
- Schema name collision: Validate uniqueness before creation
- Partition creation failure: If partition creation fails mid-way, the table should still function (queries on the parent table work, inserts into missing partitions fail)
- Large tenant migration: If a tenant schema migration is slow, other tenants should not be blocked
- Tenant decommissioning: Soft-delete the tenant record, but retain schema for compliance (configurable retention period)
- Clock skew: Use `TIMESTAMPTZ` everywhere; partition boundaries are UTC-based
- Maximum schema count: PostgreSQL handles thousands of schemas, but monitor `pg_catalog` size

---

## Dependencies

**Prerequisite Stories:**
- STORY-002: Docker Compose Development Environment (PostgreSQL container must be running)

**Blocked Stories:**
- STORY-005: API Gateway with gRPC Interceptors (needs tenant resolution)
- STORY-007: Wolverine Messaging & MQTT Broker Configuration (needs PostgreSQL for durable outbox)
- STORY-009: User Self-Registration (needs accounts table)
- STORY-010: Create Account PIN (needs accounts table)
- All stories requiring data persistence

**External Dependencies:**
- PostgreSQL 18 (via Docker container from STORY-002)
- PostGIS extension (for agent location queries) -- install in Docker image
- Npgsql EF Core provider compatible with .NET 10

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) for tenant resolution and schema switching logic
- [ ] Integration tests passing against PostgreSQL container
- [ ] Code reviewed and approved
- [ ] Documentation updated (schema diagrams, multi-tenancy guide)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
