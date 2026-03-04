# STORY-042: Message Router & Canonical Format

**Epic:** EPIC-008 National Network Switching
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 5

---

## User Story

As a **system**
I want **a message router that selects the right adapter based on destination**
So that **transactions route correctly to the appropriate national switch regardless of protocol**

---

## Description

### Background

UniBank operates in a multi-switch environment where different financial institutions connect through different national switches using different protocols. Some institutions are reachable via ISO 8583 over TCP/IP, others via ISO 20022 over REST or MQ. The Message Router is the central orchestration component in the Switching Server that abstracts this complexity.

The router works with a canonical internal message format — a protocol-agnostic representation of a financial transaction. When Core Banking needs to send a transaction to an external institution, it publishes a Wolverine command with the transaction details. The router receives this command, looks up the destination institution in the routing table to determine which adapter (ISO 8583 or ISO 20022) and which endpoint to use, then delegates to the appropriate adapter. The adapter handles all protocol-specific formatting and communication.

This design follows the Adapter Pattern, ensuring that adding support for a new switch protocol (e.g., a future proprietary API) requires only implementing a new `ISwitchAdapter` — no changes to the router, Core Banking, or any other component.

**Functional Requirements:** Supports FR-028 (Outbound Routing), FR-029 (Inbound Processing)

### Scope

**In scope:**
- `CanonicalMessage` class definition in SharedKernel with all required fields
- `MessageRouter` class that resolves the correct `ISwitchAdapter` from the institution routing table
- Institution routing table (`institution_routes`) with adapter type, endpoint, and protocol per institution
- Wolverine command handlers: `RouteOutboundTransactionHandler`, `ProcessInboundTransactionHandler`
- DI registration of adapters and router
- Adapter resolution via `IServiceProvider` keyed by adapter type
- Route caching in Redis for performance (routes change infrequently)
- Fallback routing rules when a specific institution route is not found
- Routing audit trail for troubleshooting

**Out of scope:**
- ISO 8583 adapter implementation (STORY-040)
- ISO 20022 adapter implementation (STORY-041)
- Outbound transaction flow orchestration (STORY-043)
- Inbound transaction flow orchestration (STORY-044)
- Load balancing across multiple switch endpoints for the same institution (future enhancement)

### User Flow

**Outbound Routing Flow:**
1. Core Banking publishes a `RouteOutboundTransaction` Wolverine command with canonical transaction data
2. Switching Server's `RouteOutboundTransactionHandler` receives the command
3. Handler creates a `CanonicalMessage` from the command data
4. Handler calls `MessageRouter.ResolveAdapter(destinationInstitution)`
5. Router queries the institution routing table (cache-first, DB fallback)
6. Router resolves the appropriate `ISwitchAdapter` from DI (ISO8583Adapter or ISO20022Adapter)
7. Router returns the adapter and endpoint configuration to the handler
8. Handler calls `adapter.SendAsync(canonicalMessage)` — actual sending is STORY-043

**Inbound Routing Flow:**
1. An adapter (ISO 8583 or ISO 20022) receives a raw message from the national switch
2. Adapter parses the message and calls `MessageRouter.RouteInbound(canonicalMessage)`
3. Router determines the destination module in Core Banking based on message type
4. Router publishes a `ProcessInboundTransaction` Wolverine command to Core Banking
5. Core Banking processes the transaction and returns a result
6. Router passes the result back to the adapter for response formatting

---

## Acceptance Criteria

- [ ] `CanonicalMessage` class is defined in SharedKernel with all required fields: MessageType, SourceInstitution, DestinationInstitution, Amount, Currency, SourceAccount, DestinationAccount, TransactionReference, Timestamp, and AdditionalData dictionary
- [ ] `MessageRouter` correctly resolves the appropriate `ISwitchAdapter` based on the destination institution's configured adapter type
- [ ] Institution routing table (`institution_routes`) stores adapter type, endpoint, and protocol per institution
- [ ] Router uses Redis cache for route lookups with configurable TTL (default 5 minutes)
- [ ] Cache miss falls back to database query and populates the cache
- [ ] New adapters can be registered via DI without modifying the router code (open/closed principle)
- [ ] `RouteOutboundTransaction` Wolverine command handler correctly invokes the router and delegates to the resolved adapter
- [ ] `ProcessInboundTransaction` Wolverine command handler correctly routes inbound messages to Core Banking
- [ ] Routing decision is logged with: source institution, destination institution, selected adapter type, endpoint, and timestamp
- [ ] When no route is found for an institution, a clear error is returned and logged (no silent failures)
- [ ] Fallback routing rules are applied when a specific institution route is not configured (e.g., default to national switch)

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `CanonicalMessage.cs` | `src/Shared/UniBank.SharedKernel/Switching/` | Protocol-agnostic transaction representation |
| `MessageRouter.cs` | `src/Satellites/UniBank.Switching/Routing/` | Resolves adapter and endpoint for a destination |
| `IMessageRouter.cs` | `src/Satellites/UniBank.Switching/Routing/` | Router interface for DI |
| `InstitutionRoute.cs` | `src/Satellites/UniBank.Switching/Routing/` | Entity for institution routing configuration |
| `RouteCache.cs` | `src/Satellites/UniBank.Switching/Routing/` | Redis-backed route cache |
| `RouteOutboundTransactionHandler.cs` | `src/Satellites/UniBank.Switching/Handlers/` | Wolverine handler for outbound routing |
| `ProcessInboundTransactionHandler.cs` | `src/Satellites/UniBank.Switching/Handlers/` | Wolverine handler for inbound processing |
| `SwitchingServiceRegistration.cs` | `src/Satellites/UniBank.Switching/Configuration/` | DI registration for adapters and router |
| `ISwitchAdapter.cs` | `src/Satellites/UniBank.Switching/Adapters/` | Adapter interface (shared with STORY-040/041) |

### API / gRPC Endpoints

The Message Router does not expose external endpoints. It is an internal orchestration component. Communication with Core Banking is via Wolverine messaging.

**CanonicalMessage Definition:**

```csharp
namespace UniBank.SharedKernel.Switching;

/// <summary>
/// Protocol-agnostic representation of a financial transaction.
/// Used as the lingua franca between Core Banking and the Switching Server.
/// </summary>
public class CanonicalMessage
{
    /// <summary>Transaction type: Authorization, Financial, Reversal, NetworkManagement</summary>
    public MessageType MessageType { get; set; }

    /// <summary>BIC or institution code of the originating institution</summary>
    public string SourceInstitution { get; set; } = string.Empty;

    /// <summary>BIC or institution code of the destination institution</summary>
    public string DestinationInstitution { get; set; } = string.Empty;

    /// <summary>Transaction amount in minor units (cents)</summary>
    public long Amount { get; set; }

    /// <summary>ISO 4217 currency code (e.g., ZAR, BWP, MUR)</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Source account identifier (PAN or account number)</summary>
    public string SourceAccount { get; set; } = string.Empty;

    /// <summary>Destination account identifier</summary>
    public string DestinationAccount { get; set; } = string.Empty;

    /// <summary>Unique transaction reference (maps to STAN+RRN for 8583, EndToEndId for 20022)</summary>
    public string TransactionReference { get; set; } = string.Empty;

    /// <summary>Retrieval reference number for reconciliation</summary>
    public string RetrievalReference { get; set; } = string.Empty;

    /// <summary>Transaction timestamp in UTC</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Processing code (purchase, cash advance, refund, etc.)</summary>
    public string ProcessingCode { get; set; } = string.Empty;

    /// <summary>Response code from the switch (null for outbound requests)</summary>
    public string? ResponseCode { get; set; }

    /// <summary>Authorization code from the switch (null for outbound requests)</summary>
    public string? AuthorizationCode { get; set; }

    /// <summary>Terminal identifier</summary>
    public string? TerminalId { get; set; }

    /// <summary>Merchant identifier</summary>
    public string? MerchantId { get; set; }

    /// <summary>Merchant name and location</summary>
    public string? MerchantName { get; set; }

    /// <summary>POS entry mode (chip, contactless, magnetic, manual)</summary>
    public string? POSEntryMode { get; set; }

    /// <summary>POS condition code</summary>
    public string? POSCondition { get; set; }

    /// <summary>Encrypted PIN block (binary, from HSM)</summary>
    public byte[]? PINBlock { get; set; }

    /// <summary>MAC bytes (computed by adapter via HSM)</summary>
    public byte[]? MAC { get; set; }

    /// <summary>Tenant identifier for multi-tenancy</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Correlation ID for request/response matching</summary>
    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    /// <summary>Flexible key-value store for protocol-specific data that does not fit standard fields</summary>
    public Dictionary<string, string> AdditionalData { get; set; } = new();
}

public enum MessageType
{
    Authorization,      // ISO 8583: 0100/0110
    Financial,          // ISO 8583: 0200/0210
    Reversal,           // ISO 8583: 0400/0410
    NetworkManagement,  // ISO 8583: 0800/0810
    CreditTransfer,     // ISO 20022: pacs.008
    StatusReport,       // ISO 20022: pacs.002
    Statement           // ISO 20022: camt.053
}
```

**Wolverine Commands:**

```csharp
// Published by Core Banking, handled by Switching Server
public record RouteOutboundTransaction(
    string TenantId,
    string SourceInstitution,
    string DestinationInstitution,
    string SourceAccount,
    string DestinationAccount,
    long Amount,
    string Currency,
    string ProcessingCode,
    string TransactionReference,
    string? TerminalId,
    string? MerchantId,
    string? MerchantName,
    string? POSEntryMode,
    byte[]? PINBlock,
    Dictionary<string, string>? AdditionalData
);

// Published by Switching Server, handled by Core Banking
public record ProcessInboundTransaction(
    string TenantId,
    string SourceInstitution,
    string DestinationInstitution,
    string DestinationAccount,
    long Amount,
    string Currency,
    string TransactionReference,
    string RetrievalReference,
    string ProcessingCode,
    Dictionary<string, string>? AdditionalData
);

// Published by Switching Server after outbound completes
public record OutboundTransactionResult(
    string TenantId,
    string TransactionReference,
    string ResponseCode,
    string? AuthorizationCode,
    bool Approved,
    string? DeclineReason,
    Dictionary<string, string>? AdditionalData
);
```

### Database Changes

**institution_routes table** (in the `switching` schema):

```sql
CREATE TABLE switching.institution_routes (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id      VARCHAR(20) NOT NULL,
    institution_name    VARCHAR(100) NOT NULL,
    adapter_type        VARCHAR(20) NOT NULL CHECK (adapter_type IN ('ISO8583', 'ISO20022')),
    protocol            VARCHAR(10) NOT NULL CHECK (protocol IN ('TCP', 'REST', 'MQ')),
    endpoint            VARCHAR(500) NOT NULL,
    port                INT,
    auth_type           VARCHAR(10) NOT NULL DEFAULT 'NONE',
    is_active           BOOLEAN NOT NULL DEFAULT true,
    is_default          BOOLEAN NOT NULL DEFAULT false,
    priority            INT NOT NULL DEFAULT 100,
    timeout_seconds     INT NOT NULL DEFAULT 30,
    metadata            JSONB,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (institution_id)
);

CREATE INDEX idx_institution_routes_active ON switching.institution_routes (is_active) WHERE is_active = true;
CREATE INDEX idx_institution_routes_default ON switching.institution_routes (is_default) WHERE is_default = true;

-- Seed data: default national switch route
INSERT INTO switching.institution_routes (institution_id, institution_name, adapter_type, protocol, endpoint, port, is_default, priority)
VALUES ('DEFAULT', 'National Switch (Default)', 'ISO8583', 'TCP', 'switch.national.example', 9100, true, 999);
```

**routing_audit table** (for troubleshooting routing decisions):

```sql
CREATE TABLE switching.routing_audit (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           VARCHAR(50) NOT NULL,
    transaction_ref     VARCHAR(50) NOT NULL,
    source_institution  VARCHAR(20) NOT NULL,
    dest_institution    VARCHAR(20) NOT NULL,
    resolved_adapter    VARCHAR(20),
    resolved_endpoint   VARCHAR(500),
    route_source        VARCHAR(10) NOT NULL,  -- 'cache', 'database', 'default'
    resolution_time_ms  INT NOT NULL,
    success             BOOLEAN NOT NULL,
    error_message       VARCHAR(500),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_routing_audit_tenant_time ON switching.routing_audit (tenant_id, created_at DESC);
CREATE INDEX idx_routing_audit_txn_ref ON switching.routing_audit (transaction_ref);
```

### Security Considerations

- **Route Configuration Access:** Only system administrators should be able to modify the `institution_routes` table. Route changes should be audited. Consider a Wolverine event (`InstitutionRouteChanged`) to invalidate caches and notify operations.
- **Cache Poisoning:** Redis route cache entries should use a key prefix scoped to the switching service (e.g., `switching:route:{institutionId}`). Redis access should require authentication.
- **Input Validation:** The `CanonicalMessage` must be validated before routing: all required fields present, amount non-negative, currency valid (ISO 4217), institution codes within expected format. Use FluentValidation.
- **Tenant Isolation:** Routes are global (not per-tenant) since national switch connectivity is shared infrastructure. However, the `CanonicalMessage.TenantId` must be propagated through the routing chain to ensure tenant context is preserved for logging, auditing, and multi-tenant transaction tables.

### Edge Cases

- **Unknown Institution:** If the destination institution has no entry in `institution_routes` and no default route is configured, return an error with a clear message. Log at ERROR level. Do not silently drop the transaction.
- **Default Route Fallback:** If a specific route is not found, the router falls back to the route marked `is_default = true`. If multiple defaults exist, use the one with the highest `priority` value.
- **Inactive Route:** If the resolved route has `is_active = false`, treat it as not found and fall back to the default route. Log a warning that the primary route is inactive.
- **Cache Invalidation:** When a route is added or modified in the database, the Redis cache must be invalidated. Use a Wolverine event (`InvalidateRouteCache`) to trigger cache clear across all Switching Server instances.
- **Adapter Not Registered:** If the `adapter_type` in the route refers to an adapter that is not registered in DI (e.g., a future adapter type), return a clear error rather than a DI resolution exception.
- **Concurrent Route Updates:** Use optimistic concurrency (`updated_at` check) when modifying routes to prevent lost updates.
- **Router Startup:** On Switching Server startup, pre-warm the route cache by loading all active routes from the database into Redis.

---

## Dependencies

**Prerequisite Stories:**
- STORY-040: ISO 8583 Adapter — one of the adapters the router resolves
- STORY-041: ISO 20022 Adapter — the other adapter the router resolves
- STORY-004: gRPC Proto Definitions — shared contracts

**Blocked Stories:**
- STORY-043: Outbound Transaction Routing — uses the router to send transactions out
- STORY-044: Inbound Transaction Processing — uses the router to process incoming transactions

**External Dependencies:**
- Redis instance for route caching (provisioned via Docker Compose in STORY-002)
- Wolverine messaging infrastructure (STORY-007)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — router resolution tested with mock adapters
- [ ] Integration tests passing with Redis cache and PostgreSQL routing table
- [ ] CanonicalMessage class reviewed and agreed by team (it is a shared contract)
- [ ] Wolverine command/event definitions reviewed and agreed
- [ ] Route cache hit/miss verified with Redis
- [ ] Default route fallback tested
- [ ] Unknown institution error handling tested
- [ ] Code reviewed and approved
- [ ] Documentation updated (canonical message format, routing configuration, adapter registration)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
