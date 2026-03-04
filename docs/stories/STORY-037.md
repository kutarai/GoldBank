# STORY-037: Bill Provider Registry

**Epic:** EPIC-007 Bill Payments
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a user,
I want to browse and search bill providers,
So that I can find the right provider.

---

## Description

### Background
Bill payments are a high-frequency use case for mobile money platforms in Southern Africa. Users need to pay electricity (ZESA, Eskom, BPC), water (ZINWA, Rand Water), telecoms (Econet, MTN, Vodacom), internet, insurance, and government fees. A well-organized bill provider registry is the gateway to the bill payment experience — users must be able to quickly find their provider, understand the required account format, and know the acceptable payment range.

The bill provider registry is a centralized catalog of all supported billers, organized by category. It is stored in the public schema (shared across tenants, since many providers operate across multiple SADC countries) with tenant-specific overrides for availability and limits. The registry is cached in Redis with a 1-hour TTL to reduce database load, as it changes infrequently.

Functional Requirement: **FR-024**.

### Scope

**In scope:**
- Bill providers table in public schema with provider details
- Tenant-specific provider availability configuration
- Category-based organization (electricity, water, telecom, internet, insurance, government)
- Search/filter by provider name or category
- Provider details: name, category, account format regex, min/max payment amounts, API endpoint, status
- Redis caching with 1-hour TTL per tenant
- gRPC endpoints for listing and searching providers

**Out of scope:**
- Provider API integration/connectivity testing (handled in STORY-038)
- Provider onboarding workflow (admin story)
- Dynamic provider addition via API (admin panel feature)
- Provider logo/icon management
- Provider-specific payment form customization

### User Flow
1. User opens "Pay Bills" in the mobile app
2. App calls ListProviders API; results served from Redis cache or database
3. User sees providers organized by category (Electricity, Water, Telecom, etc.)
4. User can browse categories or search by provider name
5. User selects a category to see all providers in that category
6. User selects a provider to see details: name, account number format hint, min/max amounts
7. User proceeds to enter payment details (STORY-038)

---

## Acceptance Criteria

- [ ] System maintains a registry of bill providers with: name, category, account format regex, min amount, max amount, API endpoint, and status
- [ ] Providers are organized into categories: electricity, water, telecom, internet, insurance, government
- [ ] User can retrieve a list of all active providers for their tenant
- [ ] User can filter providers by category
- [ ] User can search providers by name (case-insensitive partial match)
- [ ] Provider details include: display name, category, account number format hint (human-readable), minimum payment amount, and maximum payment amount
- [ ] Provider list is cached in Redis with key `bill_providers:{tenant_id}` and 1-hour TTL
- [ ] Cache is invalidated when a provider is added, updated, or removed
- [ ] Inactive or suspended providers are not returned in user-facing queries
- [ ] Provider availability can be configured per tenant (a provider active globally may be disabled for a specific tenant)
- [ ] API response time for provider list is under 100ms when served from cache
- [ ] Empty search results return a clear "No providers found" response, not an error

---

## Technical Notes

### Components

**Module:** `UniBank.Core/Modules/BillPay/`

```
BillPay/
  Domain/
    Entities/
      BillProvider.cs                 # Provider entity
      BillProviderCategory.cs         # Category enum
      TenantBillProvider.cs           # Tenant-specific overrides
    ValueObjects/
      AccountFormatRegex.cs           # Regex pattern for account validation
      PaymentRange.cs                 # Min/max amount value object
  Application/
    Queries/
      ListProvidersQuery.cs           # All active providers for tenant
      ListProvidersByCategoryQuery.cs # Filter by category
      SearchProvidersQuery.cs         # Search by name
      GetProviderDetailsQuery.cs      # Single provider details
    Handlers/
      ListProvidersHandler.cs
      SearchProvidersHandler.cs
      GetProviderDetailsHandler.cs
    Cache/
      BillProviderCacheService.cs     # Redis cache management
  Infrastructure/
    Persistence/
      BillProviderRepository.cs
      BillProviderEntityConfiguration.cs
    Services/
      BillProviderService.cs          # BillPayService.ListProviders
  Grpc/
    BillPayGrpcService.cs            # gRPC endpoint mapping
```

### API / gRPC Endpoints

**ListProviders:**
```protobuf
rpc ListProviders(ListProvidersRequest) returns (ListProvidersResponse);

message ListProvidersRequest {
  string category = 1;              // optional filter; empty = all categories
  string search_term = 2;           // optional name search; empty = no filter
}

message BillProviderInfo {
  string id = 1;
  string name = 2;
  string category = 3;
  string account_format_hint = 4;   // human-readable: "10-digit meter number"
  string account_format_regex = 5;  // regex for client-side validation
  string min_amount = 6;
  string max_amount = 7;
  string currency = 8;
  string status = 9;
}

message ListProvidersResponse {
  repeated BillProviderInfo providers = 1;
  map<string, int32> category_counts = 2;  // category -> count for UI badges
}
```

**GetProviderDetails:**
```protobuf
rpc GetProviderDetails(GetProviderDetailsRequest) returns (GetProviderDetailsResponse);

message GetProviderDetailsRequest {
  string provider_id = 1;
}

message GetProviderDetailsResponse {
  string id = 1;
  string name = 2;
  string category = 3;
  string account_format_hint = 4;
  string account_format_regex = 5;
  string min_amount = 6;
  string max_amount = 7;
  string currency = 8;
  string description = 9;
  repeated string accepted_currencies = 10;
  bool requires_reference = 11;     // some providers need an additional reference
  string reference_label = 12;      // "Meter Number", "Account ID", "Policy Number"
}
```

### Database Changes

**Table: `bill_providers` (public schema)**
```sql
CREATE TABLE public.bill_providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    display_name VARCHAR(150) NOT NULL,
    category VARCHAR(30) NOT NULL,
    account_format_regex VARCHAR(200) NOT NULL,
    account_format_hint VARCHAR(100) NOT NULL,
    min_amount DECIMAL(18,4) NOT NULL DEFAULT 1.00,
    max_amount DECIMAL(18,4) NOT NULL DEFAULT 100000.00,
    currency VARCHAR(3) NOT NULL,
    api_endpoint VARCHAR(500),
    api_adapter_type VARCHAR(50) NOT NULL DEFAULT 'generic',
    requires_reference BOOLEAN NOT NULL DEFAULT FALSE,
    reference_label VARCHAR(50),
    description TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'active',    -- active, inactive, suspended, maintenance
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_bill_providers_category ON public.bill_providers(category);
CREATE INDEX idx_bill_providers_status ON public.bill_providers(status);
CREATE INDEX idx_bill_providers_name ON public.bill_providers(name);
CREATE INDEX idx_bill_providers_search ON public.bill_providers USING gin(to_tsvector('english', name || ' ' || display_name));
```

**Table: `tenant_bill_providers` (public schema)**
```sql
CREATE TABLE public.tenant_bill_providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    provider_id UUID NOT NULL REFERENCES public.bill_providers(id),
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    min_amount_override DECIMAL(18,4),
    max_amount_override DECIMAL(18,4),
    commission_rate DECIMAL(18,6),
    sort_order_override INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, provider_id)
);

CREATE INDEX idx_tenant_providers_tenant ON public.tenant_bill_providers(tenant_id);
```

### Seed Data (Southern Africa Providers)

```sql
-- Electricity
INSERT INTO public.bill_providers (name, display_name, category, account_format_regex, account_format_hint, min_amount, max_amount, currency, api_adapter_type, requires_reference, reference_label) VALUES
('zesa', 'ZESA Holdings', 'electricity', '^\d{11}$', '11-digit meter number', 1.00, 50000.00, 'USD', 'zesa', true, 'Meter Number'),
('eskom', 'Eskom', 'electricity', '^\d{13}$', '13-digit meter number', 10.00, 100000.00, 'ZAR', 'eskom', true, 'Meter Number'),
('bpc', 'Botswana Power Corporation', 'electricity', '^\d{8}$', '8-digit account number', 5.00, 50000.00, 'BWP', 'generic', true, 'Account Number');

-- Water
INSERT INTO public.bill_providers (name, display_name, category, account_format_regex, account_format_hint, min_amount, max_amount, currency, api_adapter_type, requires_reference, reference_label) VALUES
('zinwa', 'ZINWA', 'water', '^\d{10}$', '10-digit account number', 1.00, 10000.00, 'USD', 'generic', true, 'Account Number'),
('rand_water', 'Rand Water', 'water', '^\d{12}$', '12-digit customer number', 20.00, 50000.00, 'ZAR', 'generic', true, 'Customer Number');

-- Telecom
INSERT INTO public.bill_providers (name, display_name, category, account_format_regex, account_format_hint, min_amount, max_amount, currency, api_adapter_type) VALUES
('econet', 'Econet Wireless', 'telecom', '^263\d{9}$', 'Phone number (263...)', 0.50, 1000.00, 'USD', 'econet'),
('mtn_za', 'MTN South Africa', 'telecom', '^27\d{9}$', 'Phone number (27...)', 5.00, 5000.00, 'ZAR', 'mtn'),
('vodacom', 'Vodacom', 'telecom', '^27\d{9}$', 'Phone number (27...)', 5.00, 5000.00, 'ZAR', 'vodacom');

-- Internet
INSERT INTO public.bill_providers (name, display_name, category, account_format_regex, account_format_hint, min_amount, max_amount, currency, api_adapter_type, requires_reference, reference_label) VALUES
('telone', 'TelOne', 'internet', '^\d{8}$', '8-digit account number', 5.00, 5000.00, 'USD', 'generic', true, 'Account Number');

-- Insurance
INSERT INTO public.bill_providers (name, display_name, category, account_format_regex, account_format_hint, min_amount, max_amount, currency, api_adapter_type, requires_reference, reference_label) VALUES
('old_mutual', 'Old Mutual', 'insurance', '^[A-Z0-9]{10}$', '10-character policy number', 10.00, 100000.00, 'ZAR', 'generic', true, 'Policy Number'),
('first_mutual', 'First Mutual', 'insurance', '^\d{8}$', '8-digit policy number', 5.00, 50000.00, 'USD', 'generic', true, 'Policy Number');
```

### Redis Caching Strategy

```csharp
public class BillProviderCacheService
{
    private const string CacheKeyPrefix = "bill_providers";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    // Cache key: bill_providers:{tenant_id}
    // Cache key with category: bill_providers:{tenant_id}:category:{category}
    // Cache key with search: not cached (search is always live but against cached provider list)

    public async Task<List<BillProviderInfo>> GetProvidersAsync(Guid tenantId, string? category = null)
    {
        var cacheKey = category != null
            ? $"{CacheKeyPrefix}:{tenantId}:category:{category}"
            : $"{CacheKeyPrefix}:{tenantId}";

        var cached = await _redis.GetAsync<List<BillProviderInfo>>(cacheKey);
        if (cached != null) return cached;

        // Cache miss: load from database
        var providers = await _repository.GetActiveProvidersAsync(tenantId, category);
        await _redis.SetAsync(cacheKey, providers, CacheTtl);

        return providers;
    }

    public async Task InvalidateCacheAsync(Guid tenantId)
    {
        // Invalidate all cache keys for this tenant
        var pattern = $"{CacheKeyPrefix}:{tenantId}*";
        await _redis.RemoveByPatternAsync(pattern);
    }

    // Search is performed in-memory against the cached full list
    public async Task<List<BillProviderInfo>> SearchProvidersAsync(Guid tenantId, string searchTerm)
    {
        var allProviders = await GetProvidersAsync(tenantId);
        return allProviders
            .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                     || p.DisplayName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
```

### Security Considerations
- **Public data:** Bill provider registry is not sensitive; no PII involved
- **Tenant filtering:** Even though providers are in public schema, tenant-specific availability is enforced
- **Cache poisoning:** Redis cache keys include tenant_id; one tenant cannot access another tenant's filtered list
- **API endpoint security:** Provider API endpoints stored in database are never exposed to end users; only used server-side
- **Input sanitization:** Search terms are sanitized to prevent injection (parameterized queries for DB, escaped for Redis)

### Edge Cases
- **No providers configured for tenant:** Return empty list with category_counts all zero; UI shows "No bill payment providers available"
- **All providers in a category disabled for tenant:** Category still appears in category list but with zero count
- **Provider status changes while cached:** Stale cache for up to 1 hour; acceptable for provider status changes which are infrequent
- **Redis unavailable:** Fall back to direct database query; log warning for monitoring
- **Very large number of providers:** Unlikely in Southern Africa market (< 100 providers); no pagination needed for provider list
- **Provider name with special characters:** Display name supports Unicode; search uses case-insensitive comparison
- **Concurrent cache invalidation and read:** Redis atomic operations prevent partial reads; worst case is a cache miss leading to DB query

---

## Dependencies

**Prerequisite Stories:**
- STORY-003: Database Schema & Multi-Tenancy Setup (public schema and tenant schema infrastructure)

**Blocked Stories:**
- STORY-038: Pay Bill (requires provider registry to select a biller)
- STORY-039: Saved/Favorite Billers (references providers from registry)

**External Dependencies:**
- Redis must be configured and accessible (STORY-002: Docker Compose)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (cache hit, cache miss, cache invalidation, search)
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
