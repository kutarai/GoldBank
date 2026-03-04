# STORY-069: Tenant Data Isolation Verification

**Epic:** EPIC-013 White-Label Configuration
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 6

---

## User Story

As a **platform owner**
I want **verified tenant data isolation**
So that **white-label deployments are fully separated and no institution can access another institution's data**

---

## Description

### Background

Multi-tenant data isolation is the single most critical security requirement for a white-label banking platform. If Tenant A (a Zambian bank) can accidentally or deliberately access Tenant B's (a Malawian bank's) customer data, the consequences are catastrophic: regulatory violations, loss of banking licenses, customer harm, and platform-wide trust destruction. UniBank's schema-per-tenant architecture provides structural isolation, but structural design alone is insufficient — it must be verified, tested, and continuously enforced.

This story implements a comprehensive verification framework that validates tenant data isolation at every layer of the stack:
1. **Application layer:** EF Core `TenantDbContext` always resolves to the correct schema via `ITenantProvider`
2. **Database layer:** PostgreSQL Row-Level Security (RLS) as an additional guard, preventing cross-tenant access even if application logic has a bug
3. **API layer:** Gateway interceptor extracts tenant ID from JWT and sets it on every gRPC call
4. **Integration test layer:** Automated tests that create data in one tenant context and attempt to access it from another — verifying access fails
5. **Cryptographic layer:** Separate HSM encryption key references per tenant
6. **Audit layer:** Any cross-tenant access attempt is logged and triggers an alert

This is not a feature that end users interact with directly. It is a security enforcement and verification framework that gives the platform owner and deploying institutions confidence that their data is isolated.

**Functional Requirement:** FR-056

### Scope

**In scope:**
- Verification that `TenantDbContext` always resolves correct PostgreSQL schema via `ITenantProvider`
- PostgreSQL Row-Level Security (RLS) policies as defense-in-depth on all tenant-scoped tables
- Gateway gRPC interceptor that extracts and propagates tenant ID from JWT on every request
- Integration test suite: create data in Tenant A, query from Tenant B context, verify empty/forbidden result
- Verification of per-tenant HSM encryption key isolation
- Audit logging and alerting for any detected cross-tenant access attempt
- Tenant isolation verification report (runnable on demand for compliance)
- Penetration test scenarios documented and automated where possible

**Out of scope:**
- Implementing the multi-tenancy infrastructure itself (covered in STORY-003 and STORY-005)
- Physical network isolation between tenants (shared infrastructure model)
- Tenant-specific database server instances (schema isolation within shared PostgreSQL)
- SOC 2 or ISO 27001 audit preparation (this story provides evidence, not the audit itself)
- Encryption at rest for PostgreSQL (handled at infrastructure layer via TDE or disk encryption)

### User Flow

This is a system verification story. The "user flows" are verification procedures:

**Automated Verification (CI/CD pipeline):**
1. Integration test suite creates two test tenants: `TEST_TENANT_A` and `TEST_TENANT_B`
2. For each data entity type (accounts, transactions, customers, terminals, etc.):
   a. Create a record in `TEST_TENANT_A` context
   b. Switch to `TEST_TENANT_B` context
   c. Attempt to query the record by ID — verify `NotFound` or empty result
   d. Attempt to list records — verify `TEST_TENANT_A` records are absent
   e. Attempt to update the record — verify `NotFound` or `Forbidden`
   f. Attempt to delete the record — verify `NotFound` or `Forbidden`
3. Verify that RLS policies are active on all tenant-scoped tables
4. Verify that the gRPC interceptor rejects requests without a valid tenant ID in the JWT
5. Verify that HSM key references for `TEST_TENANT_A` are inaccessible from `TEST_TENANT_B` context
6. Generate a verification report with pass/fail for each check

**Manual Penetration Test Scenarios:**
1. Modify JWT to change tenant_id claim — verify system rejects tampered token
2. Directly connect to PostgreSQL and attempt cross-schema query — verify RLS blocks it
3. Call gRPC endpoint with valid auth but manipulated tenant_id metadata — verify interceptor rejects
4. Attempt to reference another tenant's HSM key in a cryptographic operation — verify HSM service rejects

---

## Acceptance Criteria

- [ ] Schema-per-tenant is enforced at the application level: EF Core `TenantDbContext` always resolves the correct PostgreSQL schema via `ITenantProvider`, and no query can cross schema boundaries
- [ ] PostgreSQL Row-Level Security (RLS) is enabled on all tenant-scoped tables as defense-in-depth: `ALTER TABLE ... ENABLE ROW LEVEL SECURITY` with policy `CREATE POLICY tenant_isolation ON ... USING (tenant_id = current_setting('app.tenant_id'))`
- [ ] Cross-tenant data access is prevented at the database level: even a direct SQL query from a session with the wrong `app.tenant_id` setting returns no rows
- [ ] Tenant-specific HSM encryption key references are isolated: cryptographic operations requested for Tenant A's keys fail when called from Tenant B's context
- [ ] Tenant ID is enforced on all gRPC calls via a gateway interceptor that extracts tenant_id from the JWT and sets it as gRPC metadata
- [ ] gRPC requests without a valid tenant_id in the JWT are rejected with `Unauthenticated` status
- [ ] Integration tests verify cross-tenant access fails for all major entity types: accounts, customers, transactions, terminals, and configurations
- [ ] Any detected cross-tenant access attempt is logged in the audit trail and triggers a `CrossTenantAccessAlert` Wolverine event
- [ ] A tenant isolation verification report can be generated on demand, listing all checks and their pass/fail status
- [ ] Penetration test scenarios for cross-tenant access are documented and automated where feasible

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `TenantDbContext.cs` | `src/Shared/UniBank.SharedKernel/Tenancy/` | EF Core DbContext with tenant schema resolution |
| `ITenantProvider.cs` | `src/Shared/UniBank.SharedKernel/Tenancy/` | Interface for current tenant resolution |
| `TenantGrpcInterceptor.cs` | `src/Gateway/UniBank.Gateway/Interceptors/` | gRPC interceptor for tenant ID extraction from JWT |
| `RlsMigration.cs` | `src/Shared/UniBank.SharedKernel/Migrations/` | EF Core migration applying RLS policies |
| `TenantIsolationTests.cs` | `tests/UniBank.IntegrationTests/Tenancy/` | Cross-tenant access integration tests |
| `TenantIsolationReport.cs` | `src/Modules/UniBank.Admin/Reports/` | Generates tenant isolation verification report |
| `CrossTenantAccessAlert.cs` | `src/Shared/UniBank.Events/Security/` | Wolverine alert event for cross-tenant access attempts |
| `TenantIsolationMiddleware.cs` | `src/Shared/UniBank.SharedKernel/Tenancy/` | Middleware that sets PostgreSQL session variable |

### Application Layer Isolation

**TenantDbContext schema resolution:**

```csharp
public class TenantDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public TenantDbContext(DbContextOptions options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException("Tenant context is required but not set.");

        // All entities are mapped to the tenant-specific schema
        var schema = $"tenant_{tenantId.ToLowerInvariant()}";
        modelBuilder.HasDefaultSchema(schema);

        base.OnModelCreating(modelBuilder);
    }
}
```

**ITenantProvider implementation:**

```csharp
public class GrpcTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public string GetCurrentTenantId()
    {
        // Extract from gRPC metadata (set by TenantGrpcInterceptor)
        var tenantId = _httpContextAccessor.HttpContext?
            .Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantId))
            throw new TenantNotFoundException("Tenant ID not found in request context.");

        return tenantId;
    }
}
```

### Database Layer Isolation (RLS)

**Row-Level Security migration:**

```sql
-- Applied to every tenant-scoped table in every tenant schema
-- This is defense-in-depth: even if the application resolves the wrong schema,
-- RLS prevents data leakage at the PostgreSQL level.

-- Example for accounts table:
ALTER TABLE tenant_001.accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_001.accounts FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_accounts ON tenant_001.accounts
    USING (tenant_id = current_setting('app.tenant_id', true))
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true));

-- The application sets the session variable on each connection:
-- SET LOCAL app.tenant_id = 'TENANT-001';
-- This is done by TenantIsolationMiddleware before any query executes.
```

**TenantIsolationMiddleware (sets PostgreSQL session variable):**

```csharp
public class TenantIsolationMiddleware : IDbConnectionInterceptor
{
    private readonly ITenantProvider _tenantProvider;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Set the PostgreSQL session variable for RLS
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SET LOCAL app.tenant_id = @tenantId";
        var param = cmd.CreateParameter();
        param.ParameterName = "tenantId";
        param.Value = tenantId;
        cmd.Parameters.Add(param);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

### API Layer Isolation (gRPC Interceptor)

**TenantGrpcInterceptor:**

```csharp
public class TenantGrpcInterceptor : Interceptor
{
    private readonly ILogger<TenantGrpcInterceptor> _logger;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        // Extract tenant_id from JWT claims
        var httpContext = context.GetHttpContext();
        var tenantClaim = httpContext.User.FindFirst("tenant_id");

        if (tenantClaim == null || string.IsNullOrEmpty(tenantClaim.Value))
        {
            _logger.LogWarning("gRPC request without tenant_id claim from {Peer}", context.Peer);
            throw new RpcException(new Status(StatusCode.Unauthenticated,
                "Tenant context is required. Ensure JWT contains tenant_id claim."));
        }

        // Set tenant ID in request headers for downstream consumption
        httpContext.Request.Headers["X-Tenant-Id"] = tenantClaim.Value;

        // Verify tenant_id in request body matches JWT claim (if present)
        if (request is ITenantScoped tenantScoped
            && !string.IsNullOrEmpty(tenantScoped.TenantId)
            && tenantScoped.TenantId != tenantClaim.Value)
        {
            _logger.LogCritical(
                "Cross-tenant access attempt! JWT tenant: {JwtTenant}, Request tenant: {RequestTenant}, Peer: {Peer}",
                tenantClaim.Value, tenantScoped.TenantId, context.Peer);

            // Publish alert
            // await _bus.PublishAsync(new CrossTenantAccessAlert { ... });

            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "Tenant ID mismatch. This incident has been logged."));
        }

        return await continuation(request, context);
    }
}
```

### Integration Test Suite

**TenantIsolationTests.cs:**

```csharp
[TestFixture]
public class TenantIsolationTests : IntegrationTestBase
{
    private const string TenantA = "TEST_TENANT_A";
    private const string TenantB = "TEST_TENANT_B";

    [Test]
    public async Task Account_CreatedInTenantA_NotVisibleFromTenantB()
    {
        // Arrange: Create account in Tenant A context
        using var tenantAContext = CreateDbContext(TenantA);
        var account = new Account { /* ... */ TenantId = TenantA };
        tenantAContext.Accounts.Add(account);
        await tenantAContext.SaveChangesAsync();

        // Act: Query from Tenant B context
        using var tenantBContext = CreateDbContext(TenantB);
        var result = await tenantBContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == account.Id);

        // Assert: Should not find the account
        Assert.IsNull(result, "Tenant B should not see Tenant A's account");
    }

    [Test]
    public async Task DirectSqlQuery_CrossSchema_BlockedByRLS()
    {
        // Arrange: Create data in Tenant A schema
        // ...

        // Act: Set session to Tenant B, query Tenant A's table directly
        using var connection = CreateRawConnection();
        await connection.OpenAsync();
        await ExecuteRaw(connection, $"SET LOCAL app.tenant_id = '{TenantB}'");
        var count = await ExecuteScalar<int>(connection,
            $"SELECT COUNT(*) FROM tenant_{TenantA.ToLower()}.accounts");

        // Assert: RLS should return 0 rows even with direct schema reference
        Assert.AreEqual(0, count,
            "RLS should prevent cross-tenant data access via direct SQL");
    }

    [Test]
    public async Task GrpcRequest_TenantIdMismatch_ReturnsPermissionDenied()
    {
        // Arrange: JWT for Tenant A, request body specifies Tenant B
        var jwt = CreateJwt(TenantA);
        var request = new GetAccountRequest { TenantId = TenantB, AccountId = "..." };

        // Act & Assert
        var ex = Assert.ThrowsAsync<RpcException>(() =>
            _client.GetAccountAsync(request, headers: new Metadata { { "Authorization", $"Bearer {jwt}" } }));
        Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Test]
    public async Task HsmKeyAccess_CrossTenant_Rejected()
    {
        // Arrange: Generate key for Tenant A
        var keyRef = await _hsmService.GenerateKey(new GenerateKeyRequest
        {
            KeyType = "ZPK", TenantId = TenantA, KeyLabel = "test_zpk"
        });

        // Act: Attempt to use key from Tenant B context
        var response = await _hsmService.EncryptPINBlock(new EncryptPINBlockRequest
        {
            ZonePinKeyRef = keyRef.KeyReference, TenantId = TenantB,
            Pin = "1234", Pan = "4111111111111111"
        });

        // Assert: Should fail — key belongs to Tenant A
        Assert.IsFalse(response.Success);
        Assert.That(response.ErrorMessage, Does.Contain("tenant"));
    }

    // Additional tests for: transactions, customers, terminals, configurations,
    // terminal keys, audit logs, etc.
}
```

### Database Changes

**RLS policies applied to all tenant-scoped tables.** The following migration applies RLS to each table:

```sql
-- Function to apply RLS to a table in a specific tenant schema
CREATE OR REPLACE FUNCTION shared.apply_tenant_rls(
    schema_name TEXT,
    table_name TEXT
) RETURNS VOID AS $$
BEGIN
    EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', schema_name, table_name);
    EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', schema_name, table_name);

    -- Drop existing policy if present (idempotent)
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', schema_name, table_name);

    -- Create isolation policy
    EXECUTE format(
        'CREATE POLICY tenant_isolation ON %I.%I '
        'USING (tenant_id = current_setting(''app.tenant_id'', true)) '
        'WITH CHECK (tenant_id = current_setting(''app.tenant_id'', true))',
        schema_name, table_name
    );
END;
$$ LANGUAGE plpgsql;

-- Apply to all tenant-scoped tables across all tenant schemas
-- Called during tenant provisioning and via migration for existing tenants
-- Tables: accounts, customers, transactions, terminals, terminal_keys,
--         terminal_updates, audit_logs, configurations, etc.
```

**cross_tenant_access_log table:**

```sql
CREATE TABLE shared.cross_tenant_access_log (
    id              BIGSERIAL PRIMARY KEY,
    attempted_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    source_tenant   VARCHAR(50) NOT NULL,   -- Tenant in the JWT
    target_tenant   VARCHAR(50) NOT NULL,   -- Tenant in the request
    endpoint        VARCHAR(200) NOT NULL,  -- gRPC method or SQL query
    caller_identity VARCHAR(200) NOT NULL,  -- User or service identity
    ip_address      INET,
    user_agent      TEXT,
    blocked         BOOLEAN NOT NULL DEFAULT TRUE,
    details         JSONB
);

CREATE INDEX idx_cross_tenant_log_time ON shared.cross_tenant_access_log (attempted_at DESC);
CREATE INDEX idx_cross_tenant_log_source ON shared.cross_tenant_access_log (source_tenant);
```

### Security Considerations

- **Defense in Depth:** Tenant isolation is enforced at three independent layers: application (EF Core schema resolution), database (PostgreSQL RLS), and API (gRPC interceptor). A bug in one layer does not compromise isolation because the other layers catch it.
- **RLS as Safety Net:** Even if a developer accidentally writes a raw SQL query that references another tenant's schema, RLS prevents data from being returned. This is the "last line of defense" principle.
- **Forced RLS:** `FORCE ROW LEVEL SECURITY` ensures that table owners (the database role used by the application) are also subject to RLS. Without FORCE, table owners bypass RLS policies.
- **JWT Integrity:** Tenant ID comes from the JWT, which is signed by the authentication service. JWT tampering is detected by signature verification at the gateway level. The gRPC interceptor trusts the JWT only after signature verification.
- **Cross-Tenant Alert Severity:** A cross-tenant access attempt is treated as a critical security incident. The `CrossTenantAccessAlert` triggers immediate notification to the platform security team, regardless of time of day.
- **HSM Key Isolation:** HSM key labels include the tenant ID. The HSM Interface validates that the caller's tenant matches the key's tenant prefix before performing any operation. This prevents one tenant from using another tenant's encryption keys.
- **Audit Immutability:** The `cross_tenant_access_log` table should be write-only from the application's perspective. Consider using a separate database role for the audit log table that only has INSERT permissions.
- **Regular Verification:** The tenant isolation report should be run as part of every deployment to staging and production. Any failure blocks the deployment.

### Edge Cases

- **New table added without RLS:** The CI/CD pipeline should include a verification step that checks all tables in tenant schemas have RLS enabled. If a developer adds a new table without applying the RLS migration, the pipeline fails.
- **Superuser bypass:** PostgreSQL superusers bypass RLS. Ensure the application database role is NOT a superuser. Document that direct database access with superuser credentials must follow strict operational procedures.
- **Null tenant_id:** If `app.tenant_id` session variable is not set, the RLS policy evaluates `current_setting('app.tenant_id', true)` which returns NULL. The comparison `tenant_id = NULL` returns false for all rows, effectively denying all access. This is the safe default.
- **Tenant deletion:** When a tenant is decommissioned, their schema should be dropped (after a retention period). RLS policies on the schema are automatically removed with the schema.
- **Shared tables:** Some tables (e.g., `shared.tenants`, `shared.system_config`) are not tenant-scoped. These tables do NOT have RLS and are accessed by platform-level services only. Ensure no sensitive per-tenant data is stored in shared tables.
- **Connection pooling:** When using connection pooling (e.g., PgBouncer), the `SET LOCAL app.tenant_id` command is scoped to the transaction. After the transaction commits, the setting is reset. Ensure every transaction begins with the correct tenant setting.
- **Migration race condition:** Applying RLS during a migration while the application is serving requests requires careful ordering: create the policy before enabling RLS enforcement, use `CREATE POLICY IF NOT EXISTS` for idempotency.
- **Performance impact:** RLS adds a filter predicate to every query on affected tables. Ensure the `tenant_id` column is indexed and the query planner uses the index. Test query performance with RLS enabled on large datasets.

---

## Dependencies

**Prerequisite Stories:**
- STORY-003: Tenant Management & Multi-Tenancy — tenant infrastructure, schema-per-tenant, `ITenantProvider`
- STORY-005: Database Infrastructure — PostgreSQL setup with schema-per-tenant capability

**Blocked Stories:**
- All future stories inherit the isolation verification framework — new entity types must be added to the test suite
- Compliance and audit stories depend on the isolation report

**External Dependencies:**
- PostgreSQL 18 with Row-Level Security support (available since PostgreSQL 9.5)
- SoftHSM2 for HSM key isolation testing in CI
- CI/CD pipeline integration for automated isolation verification on every build

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — interceptor logic, tenant provider, RLS migration helpers
- [ ] Integration tests passing — full cross-tenant isolation verification for all entity types (accounts, customers, transactions, terminals, keys, configurations)
- [ ] PostgreSQL RLS policies applied and verified on all tenant-scoped tables
- [ ] gRPC interceptor rejects requests without tenant_id and detects tenant mismatch
- [ ] HSM key isolation verified — cross-tenant key access rejected
- [ ] Cross-tenant access logging and alerting verified
- [ ] Tenant isolation report runs successfully and all checks pass
- [ ] CI/CD pipeline includes RLS verification step (fails if any table lacks RLS)
- [ ] Code reviewed and approved
- [ ] Documentation updated (isolation architecture, verification procedures, penetration test scenarios)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
