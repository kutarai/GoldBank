# STORY-070: Per-Tenant Fee & Limit Configuration

**Epic:** EPIC-013 White-Label Configuration
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 6

---

## User Story

As a **deploying institution**
I want **independent fee and limit settings**
So that **my business model is configured correctly and my revenue, risk, and compliance requirements are met**

---

## Description

### Background

Every deploying institution on the GoldBank platform operates under different regulatory regimes, market conditions, and business models. A Zambian mobile money operator might charge flat fees on NFC payments to compete with established MNOs, while a Malawian bank might use percentage-based fees to scale revenue with transaction values. A South African institution might set high daily transaction limits for their urban customer base, while a Mozambican institution might enforce lower limits due to regulatory AML requirements.

The per-tenant fee and limit configuration system makes this possible without code changes. Each tenant has its own fee schedule, transaction limits, and agent commission rates stored as structured configuration. The `SharedKernel.FeeCalculator` and `SharedKernel.LimitChecker` components read from this configuration at transaction time, ensuring that every transaction is processed according to the deploying institution's specific rules.

Configuration is cached in Redis for performance — fee calculation happens on every transaction, so database lookups are not acceptable in the hot path. The 5-minute TTL ensures changes propagate reasonably quickly. Admin endpoints allow institutions to update their configuration, and changes take effect within the cache TTL window.

This is a foundational story for the white-label proposition: without tenant-specific fees and limits, every institution would be locked into the same pricing model, eliminating the commercial flexibility that makes GoldBank attractive to diverse Southern African financial institutions.

**Functional Requirement:** FR-057

### Scope

**In scope:**
- Per-tenant fee structure configuration for all payment types: NFC, QR, P2P transfer, cash-in, cash-out, bill pay
- Per-tenant transaction limits: daily, monthly, single transaction, daily cash-out
- Per-tenant agent commission rates: cash-in rate, cash-out rate
- Per-tenant PIN thresholds: NFC PIN threshold, transfer PIN threshold
- `SharedKernel.FeeCalculator` component that reads tenant configuration and computes fees
- `SharedKernel.LimitChecker` component that validates transactions against tenant limits
- Redis caching of tenant fee/limit configuration with 5-minute TTL
- `AdminService.UpdateTenantConfig` gRPC endpoint for configuration management
- Fee simulation endpoint (calculate fee for a hypothetical transaction without processing it)
- Configuration validation (no negative fees, limits must be positive, rates must be 0-100%)
- Configuration history and audit trail

**Out of scope:**
- Fee calculation for inter-tenant transactions (future cross-border story)
- Dynamic pricing based on volume tiers (future enhancement)
- Fee waiver or promotional pricing engine (future enhancement)
- Agent onboarding and management (separate epic)
- Regulatory limit enforcement (this story configures limits; regulatory compliance monitoring is separate)

### User Flow

**Admin configures fees and limits:**
1. Admin logs into the admin portal for their institution
2. Admin navigates to Settings > Fees & Limits
3. Admin sees the current fee schedule, transaction limits, agent commission rates, and PIN thresholds
4. Admin updates the NFC payment fee from flat R2.50 to 1.5% (percentage-based)
5. Admin increases the daily transaction limit from R10,000 to R25,000
6. Admin adjusts the cash-out agent commission from 1.5% to 2.0%
7. Admin clicks "Save" — configuration is validated and saved
8. Within 5 minutes, all transactions for this tenant use the updated fees and limits

**Transaction fee calculation (runtime):**
1. Customer initiates an NFC payment of R500 at a merchant
2. PaymentService resolves the customer's tenant ID from their account
3. PaymentService calls `FeeCalculator.CalculateFee(tenantId, "nfc", 500.00)`
4. FeeCalculator fetches tenant config from Redis (or PostgreSQL on cache miss)
5. FeeCalculator applies the NFC fee rule: 1.5% of R500 = R7.50
6. Total charged to customer: R507.50 (R500 to merchant, R7.50 fee to institution)

**Transaction limit check (runtime):**
1. Customer initiates a P2P transfer of R15,000
2. PaymentService calls `LimitChecker.ValidateTransaction(tenantId, customerId, "p2p", 15000.00)`
3. LimitChecker fetches tenant config from Redis
4. LimitChecker checks: single transaction limit (R50,000 — pass), daily limit (R25,000 — checks today's total), monthly limit (R100,000 — checks this month's total)
5. If all limits pass, transaction proceeds. If any limit is exceeded, transaction is rejected with a clear message indicating which limit was hit.

---

## Acceptance Criteria

- [ ] Each tenant has a unique fee structure configurable via `AdminService.UpdateTenantConfig` gRPC endpoint
- [ ] Fee types supported: flat fee (fixed amount) and percentage fee for each payment type: NFC, QR, P2P, cash-in, cash-out, bill pay
- [ ] Transaction limits are independently configurable per tenant: daily transaction total, monthly transaction total, single transaction maximum, daily cash-out maximum
- [ ] Agent commission rates are configurable per tenant: cash-in percentage, cash-out percentage
- [ ] PIN thresholds are configurable per tenant: NFC PIN threshold amount, transfer PIN threshold amount
- [ ] `SharedKernel.FeeCalculator` correctly computes fees based on tenant configuration for all payment types
- [ ] `SharedKernel.LimitChecker` correctly validates transactions against tenant-specific limits (daily, monthly, single)
- [ ] Changes to fee and limit configuration apply only to the specified tenant — other tenants are unaffected
- [ ] Configuration is cached in Redis with key pattern `tenant_config:{tenant_id}` and 5-minute TTL
- [ ] Configuration changes take effect within 5 minutes (cache TTL) without requiring system restart
- [ ] All configuration changes are logged in the audit trail with: timestamp, admin user, tenant_id, previous values, new values
- [ ] Invalid configuration is rejected: no negative fees, limits must be positive, percentage rates must be 0-100%

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `FeeCalculator.cs` | `src/Shared/GoldBank.SharedKernel/Finance/` | Calculates transaction fees based on tenant config |
| `LimitChecker.cs` | `src/Shared/GoldBank.SharedKernel/Finance/` | Validates transactions against tenant limits |
| `TenantFeeConfig.cs` | `src/Shared/GoldBank.SharedKernel/Tenancy/` | Fee configuration value object |
| `TenantLimitConfig.cs` | `src/Shared/GoldBank.SharedKernel/Tenancy/` | Limit configuration value object |
| `TenantConfigService.cs` | `src/Modules/GoldBank.Admin/Services/` | gRPC service for tenant config management |
| `TenantConfigCache.cs` | `src/Shared/GoldBank.SharedKernel/Caching/` | Redis caching for tenant config (shared with STORY-068) |
| `FeeSimulationService.cs` | `src/Modules/GoldBank.Admin/Services/` | Fee simulation for admin preview |
| `TenantConfigHistory.cs` | `src/Modules/GoldBank.Admin/Domain/` | Configuration version history entity |

### API / gRPC Endpoints

**Proto definition** (additions to `admin_service.proto`):

```protobuf
service AdminConfigService {
  rpc GetTenantConfig (GetTenantConfigRequest) returns (GetTenantConfigResponse);
  rpc UpdateTenantConfig (UpdateTenantConfigRequest) returns (UpdateTenantConfigResponse);
  rpc SimulateFee (SimulateFeeRequest) returns (SimulateFeeResponse);
  rpc GetConfigHistory (GetConfigHistoryRequest) returns (GetConfigHistoryResponse);
  rpc RevertConfig (RevertConfigRequest) returns (UpdateTenantConfigResponse);
}

message GetTenantConfigRequest {
  string tenant_id = 1;
}

message GetTenantConfigResponse {
  TransactionFees transaction_fees = 1;
  TransactionLimits transaction_limits = 2;
  AgentCommission agent_commission = 3;
  PinThresholds pin_thresholds = 4;
  string config_version = 5;
  int64 last_updated_unix = 6;
}

message TransactionFees {
  FeeRule nfc = 1;
  FeeRule qr = 2;
  FeeRule p2p = 3;
  FeeRule cash_in = 4;
  FeeRule cash_out = 5;
  FeeRule bill_pay = 6;
}

message FeeRule {
  double flat_fee = 1;            // Fixed fee amount (e.g., 2.50)
  double percent_fee = 2;         // Percentage fee (e.g., 1.5 for 1.5%)
  double min_fee = 3;             // Minimum fee (floor)
  double max_fee = 4;             // Maximum fee (cap), 0 = no cap
  string currency_code = 5;       // ISO 4217, e.g., "ZAR"
}

message TransactionLimits {
  double daily_transaction = 1;   // Max total daily transaction amount
  double monthly_transaction = 2; // Max total monthly transaction amount
  double single_transaction = 3;  // Max single transaction amount
  double daily_cash_out = 4;      // Max daily cash-out amount
}

message AgentCommission {
  double cash_in_rate = 1;        // Commission percentage for cash-in (e.g., 1.5)
  double cash_out_rate = 2;       // Commission percentage for cash-out (e.g., 2.0)
}

message PinThresholds {
  double nfc_pin_threshold = 1;   // Amount above which NFC requires PIN (e.g., 500.00)
  double transfer_pin_threshold = 2; // Amount above which transfer requires PIN (e.g., 100.00)
}

message UpdateTenantConfigRequest {
  string tenant_id = 1;
  TransactionFees transaction_fees = 2;
  TransactionLimits transaction_limits = 3;
  AgentCommission agent_commission = 4;
  PinThresholds pin_thresholds = 5;
}

message UpdateTenantConfigResponse {
  bool success = 1;
  string error_message = 2;
  string config_version = 3;
  repeated string validation_errors = 4; // Specific field-level errors
}

message SimulateFeeRequest {
  string tenant_id = 1;
  string payment_type = 2;        // "nfc", "qr", "p2p", "cash_in", "cash_out", "bill_pay"
  double amount = 3;
  string currency_code = 4;
}

message SimulateFeeResponse {
  double transaction_amount = 1;
  double fee_amount = 2;
  double total_amount = 3;        // transaction_amount + fee_amount
  string fee_breakdown = 4;       // "1.5% of R500.00 = R7.50" or "Flat fee: R2.50"
  double agent_commission = 5;    // If applicable (cash-in/cash-out)
}

message GetConfigHistoryRequest {
  string tenant_id = 1;
  int32 page = 2;
  int32 page_size = 3;
}

message GetConfigHistoryResponse {
  repeated ConfigVersion versions = 1;
  int32 total_count = 2;
}

message ConfigVersion {
  string version_id = 1;
  string changed_by = 2;
  int64 changed_at_unix = 3;
  string change_summary = 4;
}

message RevertConfigRequest {
  string tenant_id = 1;
  string version_id = 2;
}
```

### FeeCalculator Implementation

```csharp
public class FeeCalculator : IFeeCalculator
{
    private readonly ITenantConfigCache _configCache;

    public async Task<FeeResult> CalculateFee(
        string tenantId, string paymentType, decimal amount, string currencyCode)
    {
        var config = await _configCache.GetTenantConfigAsync(tenantId);
        var feeRule = GetFeeRule(config.TransactionFees, paymentType);

        if (feeRule == null)
            throw new ConfigurationException($"No fee rule configured for {paymentType} in tenant {tenantId}");

        // Calculate fee: flat + percentage
        var calculatedFee = (decimal)feeRule.FlatFee + (amount * (decimal)feeRule.PercentFee / 100m);

        // Apply floor
        if (feeRule.MinFee > 0 && calculatedFee < (decimal)feeRule.MinFee)
            calculatedFee = (decimal)feeRule.MinFee;

        // Apply cap
        if (feeRule.MaxFee > 0 && calculatedFee > (decimal)feeRule.MaxFee)
            calculatedFee = (decimal)feeRule.MaxFee;

        // Round to 2 decimal places (banker's rounding)
        calculatedFee = Math.Round(calculatedFee, 2, MidpointRounding.ToEven);

        return new FeeResult
        {
            TransactionAmount = amount,
            FeeAmount = calculatedFee,
            TotalAmount = amount + calculatedFee,
            FeeBreakdown = BuildBreakdown(feeRule, amount, calculatedFee),
            CurrencyCode = currencyCode
        };
    }

    private FeeRule GetFeeRule(TransactionFees fees, string paymentType) => paymentType switch
    {
        "nfc" => fees.Nfc,
        "qr" => fees.Qr,
        "p2p" => fees.P2P,
        "cash_in" => fees.CashIn,
        "cash_out" => fees.CashOut,
        "bill_pay" => fees.BillPay,
        _ => throw new ArgumentException($"Unknown payment type: {paymentType}")
    };
}
```

### LimitChecker Implementation

```csharp
public class LimitChecker : ILimitChecker
{
    private readonly ITenantConfigCache _configCache;
    private readonly ITransactionRepository _transactionRepo;

    public async Task<LimitCheckResult> ValidateTransaction(
        string tenantId, string customerId, string paymentType, decimal amount)
    {
        var config = await _configCache.GetTenantConfigAsync(tenantId);
        var limits = config.TransactionLimits;
        var errors = new List<string>();

        // Check single transaction limit
        if (limits.SingleTransaction > 0 && amount > (decimal)limits.SingleTransaction)
        {
            errors.Add($"Amount {amount:C} exceeds single transaction limit of {(decimal)limits.SingleTransaction:C}");
        }

        // Check daily transaction limit
        if (limits.DailyTransaction > 0)
        {
            var todayTotal = await _transactionRepo.GetDailyTotalAsync(tenantId, customerId);
            if (todayTotal + amount > (decimal)limits.DailyTransaction)
            {
                errors.Add($"Transaction would exceed daily limit of {(decimal)limits.DailyTransaction:C}. Today's total: {todayTotal:C}");
            }
        }

        // Check monthly transaction limit
        if (limits.MonthlyTransaction > 0)
        {
            var monthTotal = await _transactionRepo.GetMonthlyTotalAsync(tenantId, customerId);
            if (monthTotal + amount > (decimal)limits.MonthlyTransaction)
            {
                errors.Add($"Transaction would exceed monthly limit of {(decimal)limits.MonthlyTransaction:C}. This month's total: {monthTotal:C}");
            }
        }

        // Check daily cash-out limit (only for cash-out transactions)
        if (paymentType == "cash_out" && limits.DailyCashOut > 0)
        {
            var todayCashOut = await _transactionRepo.GetDailyCashOutTotalAsync(tenantId, customerId);
            if (todayCashOut + amount > (decimal)limits.DailyCashOut)
            {
                errors.Add($"Transaction would exceed daily cash-out limit of {(decimal)limits.DailyCashOut:C}. Today's cash-out: {todayCashOut:C}");
            }
        }

        return new LimitCheckResult
        {
            IsWithinLimits = errors.Count == 0,
            ViolationMessages = errors
        };
    }
}
```

### Database Changes

**tenant_config table** (or config_json column on tenants table):

```sql
-- Option A: Dedicated config table (preferred for audit/history)
CREATE TABLE shared.tenant_configs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(50) NOT NULL UNIQUE,
    config_json     JSONB NOT NULL DEFAULT '{}'::JSONB,
    config_version  INT NOT NULL DEFAULT 1,
    updated_by      VARCHAR(100),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_tenant_configs_tenant ON shared.tenant_configs (tenant_id);
```

**Configuration JSON structure:**

```json
{
  "transaction_fees": {
    "nfc": { "flat_fee": 0, "percent_fee": 1.5, "min_fee": 1.00, "max_fee": 50.00, "currency_code": "ZAR" },
    "qr": { "flat_fee": 0, "percent_fee": 1.0, "min_fee": 0.50, "max_fee": 30.00, "currency_code": "ZAR" },
    "p2p": { "flat_fee": 2.50, "percent_fee": 0, "min_fee": 0, "max_fee": 0, "currency_code": "ZAR" },
    "cash_in": { "flat_fee": 0, "percent_fee": 0.5, "min_fee": 1.00, "max_fee": 25.00, "currency_code": "ZAR" },
    "cash_out": { "flat_fee": 5.00, "percent_fee": 1.0, "min_fee": 5.00, "max_fee": 100.00, "currency_code": "ZAR" },
    "bill_pay": { "flat_fee": 3.00, "percent_fee": 0, "min_fee": 0, "max_fee": 0, "currency_code": "ZAR" }
  },
  "limits": {
    "daily_transaction": 25000.00,
    "monthly_transaction": 100000.00,
    "single_transaction": 50000.00,
    "daily_cash_out": 10000.00
  },
  "agent_commission": {
    "cash_in_rate": 1.5,
    "cash_out_rate": 2.0
  },
  "pin_thresholds": {
    "nfc_pin_threshold": 500.00,
    "transfer_pin_threshold": 100.00
  }
}
```

**tenant_config_history table:**

```sql
CREATE TABLE shared.tenant_config_history (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(50) NOT NULL,
    version_number  INT NOT NULL,
    config_json     JSONB NOT NULL,
    changed_by      VARCHAR(100) NOT NULL,
    change_summary  TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, version_number)
);

CREATE INDEX idx_config_history_tenant ON shared.tenant_config_history (tenant_id, version_number DESC);
```

### Redis Caching Strategy

```
Key:     tenant_config:{tenant_id}
Value:   Serialized TenantConfiguration JSON (fees + limits + commission + thresholds)
TTL:     300 seconds (5 minutes)
Evict:   On config update (explicit cache invalidation via DEL)
```

The `TenantConfigCache` is shared between this story (fees/limits) and STORY-068 (branding). Both read from the same cached configuration object, reducing Redis round-trips.

```csharp
public class TenantConfigCache : ITenantConfigCache
{
    private readonly IDistributedCache _redis;
    private readonly ITenantConfigRepository _repository;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<TenantConfiguration> GetTenantConfigAsync(string tenantId)
    {
        var cacheKey = $"tenant_config:{tenantId}";
        var cached = await _redis.GetStringAsync(cacheKey);

        if (cached != null)
            return JsonSerializer.Deserialize<TenantConfiguration>(cached);

        var config = await _repository.GetConfigAsync(tenantId);
        if (config == null)
            config = TenantConfiguration.Default; // Safe default

        await _redis.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(config),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });

        return config;
    }

    public async Task InvalidateCacheAsync(string tenantId)
    {
        var cacheKey = $"tenant_config:{tenantId}";
        await _redis.RemoveAsync(cacheKey);
    }
}
```

### Security Considerations

- **Tenant Isolation:** Fee and limit configuration is strictly scoped to the tenant. The `UpdateTenantConfig` endpoint validates that the admin's JWT tenant matches the target tenant. Cross-tenant configuration access is blocked by the gRPC interceptor (STORY-069).
- **Validation:** All configuration values are validated server-side: no negative fees, percentage rates must be 0-100%, limits must be positive, currency codes must be valid ISO 4217. Client-side validation is for UX only — the server is the authority.
- **Audit Trail:** Every configuration change is logged with: who changed it, what was changed (diff between old and new values), when it was changed. This provides a complete audit trail for regulatory compliance.
- **Default Configuration:** If a tenant has no configuration, safe defaults are applied: zero fees (no charges), maximum limits (no restrictions), zero commission. This ensures that missing configuration does not block transactions. The deploying institution is expected to configure their fees before going live.
- **Cache Poisoning:** Redis cache keys are constructed with sanitized tenant IDs (validated alphanumeric pattern). Even if an attacker could manipulate the tenant ID, the Redis key pattern prevents directory traversal or key injection.
- **Financial Accuracy:** Fee calculations use `decimal` type (not `float`/`double`) to avoid floating-point precision errors. Rounding uses banker's rounding (`MidpointRounding.ToEven`) per ISO financial conventions.

### Edge Cases

- **Tenant with no configuration:** Default configuration is applied with zero fees and maximum limits. A warning is logged indicating the tenant is using default configuration — this prompts the operations team to configure fees before the institution goes live.
- **Fee results in negative amount:** If a fee rule somehow produces a negative fee (e.g., flat fee of -1), the system clamps the fee to zero. Negative fees (rebates) are not supported in this version.
- **Limit check with no transaction history:** If a customer has no transactions for the day/month (new customer or start of period), the accumulated total is zero, and any transaction within the single transaction limit passes.
- **Currency mismatch:** The fee currency must match the transaction currency. If they differ, the transaction is rejected with an error. Multi-currency fee calculation is out of scope for this story.
- **Concurrent configuration update:** Optimistic concurrency using `config_version`. If two admins update simultaneously, the second update receives a conflict error and must reload before retrying.
- **Redis unavailable during transaction:** If Redis is down, the `TenantConfigCache` falls back to a direct PostgreSQL query. The transaction is not blocked — it proceeds with slightly higher latency. A background alert notifies infrastructure.
- **Very high transaction volume:** The `LimitChecker` queries the transaction repository for daily/monthly totals. These queries must use indexed columns. For high-volume tenants, consider materialized daily/monthly counters updated by Wolverine events rather than aggregating on every transaction.
- **Configuration takes effect mid-transaction:** If a config update propagates during a batch of related transactions, some may use old fees and others new. This is acceptable — the 5-minute cache TTL is the documented propagation window.
- **Zero fee with minimum fee:** If flat_fee = 0, percent_fee = 0, but min_fee = 1.00, the calculated fee is 0 which is below min_fee, so the fee is 1.00. This is the correct behavior — min_fee acts as a floor.

---

## Dependencies

**Prerequisite Stories:**
- STORY-060: Fee & Limit Infrastructure (provides the base fee/limit framework that this story makes tenant-configurable)

**Blocked Stories:**
- All payment processing stories (NFC, QR, P2P, cash-in, cash-out, bill pay) depend on `FeeCalculator` and `LimitChecker` using correct tenant configuration
- Agent commission calculation stories depend on per-tenant commission rates
- PIN threshold stories depend on per-tenant PIN threshold configuration

**External Dependencies:**
- Redis instance for configuration caching
- Transaction repository for daily/monthly total aggregation in limit checks

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — fee calculation for all payment types, limit checking for all limit types, configuration validation, cache behavior
- [ ] Integration tests passing — full configuration CRUD with Redis cache and PostgreSQL, fee simulation endpoint
- [ ] Fee calculation tested for each payment type with both flat and percentage fees, including min/max fee caps
- [ ] Limit checking tested for daily, monthly, single transaction, and daily cash-out limits
- [ ] Tenant isolation verified — configuration changes for Tenant A do not affect Tenant B
- [ ] Default configuration verified — tenant without configuration uses safe defaults
- [ ] Configuration history and revert tested
- [ ] Financial precision verified — no floating-point rounding errors in fee calculations
- [ ] Code reviewed and approved
- [ ] Documentation updated (fee configuration guide, limit configuration guide)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
