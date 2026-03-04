# STORY-016: Account Balance Inquiry with Redis Caching

**Epic:** EPIC-002
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a user
I want to see my current balance instantly
So that I know how much money I have

---

## Description

### Background
Balance inquiry is the most frequently accessed feature in any mobile banking application. For UniBank's target users -- many of whom are newly banked -- having instant, reliable access to their account balance is fundamental to building trust in the platform. Users check their balance before making payments, after receiving money, and simply to reassure themselves their funds are secure.

To meet the performance requirement of sub-500ms response times, even under high concurrent load, this feature implements a Redis caching layer. The balance is cached with a 5-second TTL, which provides a good balance between performance and data freshness. The cache is proactively invalidated whenever a `TransactionCompleted` event is published, ensuring users see an updated balance immediately after any transaction.

The response distinguishes between `available_balance` (what the user can spend right now) and `ledger_balance` (the accounting balance including pending transactions), which is important for financial transparency and helps users understand holds or pending transactions.

### Scope
**In scope:**
- `GetBalance` gRPC endpoint returning current account balance
- Redis caching with 5-second TTL
- Cache key pattern: `balance:{tenant_id}:{account_id}`
- Cache invalidation on `TransactionCompleted` Wolverine event
- Fallback to database on cache miss
- Manual refresh capability (bypass cache, read from DB, repopulate cache)
- Response includes both `available_balance` and `ledger_balance`
- Response time target: < 500ms (p95)

**Out of scope:**
- Balance history or trend graphs
- Multi-currency balance display (single primary currency per account for now)
- Balance alerts or notifications (e.g., "balance below X")
- Balance display in different currencies (conversion)
- Mini-statement alongside balance

### User Flow
1. User opens the app home screen
2. App calls `AccountService.GetBalance` with the user's account ID
3. Server checks Redis cache for key `balance:{tenant_id}:{account_id}`
4. **Cache hit:** Return cached balance immediately (< 10ms)
5. **Cache miss:** Query `account_balances` table, populate cache with 5s TTL, return balance
6. App displays `available_balance` prominently on the home screen
7. `ledger_balance` displayed in a secondary position (or accessible via "details")
8. User can pull-to-refresh to force a fresh balance read
9. On pull-to-refresh, app calls `GetBalance` with `force_refresh = true`
10. Server bypasses cache, reads from DB, updates cache, returns fresh balance

**Cache Invalidation Flow:**
1. Any transaction completes (deposit, withdrawal, transfer, payment)
2. `TransactionCompleted` event is published via Wolverine
3. `BalanceCacheInvalidationHandler` receives the event
4. Handler deletes the Redis key `balance:{tenant_id}:{account_id}`
5. Next `GetBalance` call will read from DB and repopulate the cache

---

## Acceptance Criteria

- [ ] `GetBalance` returns both `available_balance` and `ledger_balance`
- [ ] Balance is served from Redis cache when available (cache hit)
- [ ] Redis cache key follows the pattern `balance:{tenant_id}:{account_id}`
- [ ] Cache TTL is 5 seconds
- [ ] Cache miss triggers a database read and populates the cache
- [ ] Cache is invalidated when a `TransactionCompleted` event is received for the account
- [ ] Manual refresh (`force_refresh = true`) bypasses the cache and reads directly from the database
- [ ] Response time is under 500ms at the 95th percentile under normal load
- [ ] If Redis is unavailable, the service falls back to database reads without errors
- [ ] Balance response includes the currency code and last updated timestamp
- [ ] Only the authenticated account owner can query their own balance

---

## Technical Notes

### Components
- **AccountModule** (`src/Modules/Account/`):
  - `AccountService.cs`: Add `GetBalance` gRPC method
  - `BalanceService.cs`: Business logic with cache-aside pattern
  - `BalanceCacheService.cs`: Redis cache read/write/invalidate operations
  - `BalanceRepository.cs`: Database access for balance records
- **Wolverine Handlers** (`src/Modules/Account/Handlers/`):
  - `BalanceCacheInvalidationHandler.cs`: Listens for `TransactionCompleted` to invalidate cache
- **Infrastructure** (`src/Infrastructure/`):
  - `RedisConnectionFactory.cs`: Redis connection management (StackExchange.Redis)

### API / gRPC Endpoints

**Service:** `AccountService`

```protobuf
service AccountService {
  rpc GetBalance(GetBalanceRequest) returns (GetBalanceResponse);
}

message GetBalanceRequest {
  string account_id = 1;
  bool force_refresh = 2;               // Bypass cache if true
}

message GetBalanceResponse {
  string account_id = 1;
  string currency = 2;                  // ISO 4217 (e.g., "MWK", "ZMW", "MZN")
  string available_balance = 3;         // String to avoid floating point issues
  string ledger_balance = 4;            // String for precision
  bool is_cached = 5;                   // Indicates if served from cache
  google.protobuf.Timestamp last_updated = 6;
  google.protobuf.Timestamp cache_timestamp = 7;
}
```

**Balance Service Logic (Cache-Aside Pattern):**

```csharp
public class BalanceService
{
    public async Task<BalanceResponse> GetBalance(string accountId, string tenantId, bool forceRefresh)
    {
        var cacheKey = $"balance:{tenantId}:{accountId}";

        if (!forceRefresh)
        {
            var cached = await _redisCache.GetAsync<BalanceResponse>(cacheKey);
            if (cached != null)
                return cached with { IsCached = true };
        }

        // Cache miss or force refresh
        var balance = await _balanceRepository.GetByAccountId(accountId);

        var response = MapToResponse(balance);
        await _redisCache.SetAsync(cacheKey, response, TimeSpan.FromSeconds(5));

        return response with { IsCached = false };
    }
}
```

**Cache Invalidation Handler:**

```csharp
public class BalanceCacheInvalidationHandler
{
    public async Task Handle(TransactionCompleted @event, IBalanceCacheService cacheService)
    {
        // Invalidate both sender and receiver caches
        await cacheService.InvalidateAsync(@event.TenantId, @event.SourceAccountId);

        if (@event.DestinationAccountId != null)
            await cacheService.InvalidateAsync(@event.TenantId, @event.DestinationAccountId);
    }
}
```

### Database Changes

No new tables required. This story reads from the `account_balances` table created in STORY-013.

**Ensure indexes exist:**

```sql
-- Should already exist from STORY-013
CREATE INDEX IF NOT EXISTS idx_account_balances_account_id ON account_balances(account_id);
```

### Redis Schema

```
Key:     balance:{tenant_id}:{account_id}
Value:   JSON serialized BalanceCacheEntry
TTL:     5 seconds

BalanceCacheEntry:
{
  "account_id": "uuid",
  "currency": "MWK",
  "available_balance": "1500.00",
  "ledger_balance": "1500.00",
  "last_updated": "2026-02-24T10:30:00Z",
  "cached_at": "2026-02-24T10:30:01Z"
}
```

### Security Considerations
- **Authorization:** Balance can only be queried by the account owner. JWT `sub` claim must match the `account_id` in the request.
- **Data in Cache:** Redis should be configured with TLS for data in transit and password authentication. Balance data in Redis is ephemeral (5s TTL) but still sensitive.
- **Cache Poisoning:** The cache is only populated by the service itself (not by external writes). Redis access is restricted to the application's internal network.
- **Precision:** Balances stored and transmitted as strings (not floating point) to prevent precision loss. Internal representation uses `decimal` (C#) / `DECIMAL(18,2)` (PostgreSQL).
- **Rate Limiting:** Balance inquiry limited to 60 requests per minute per account to prevent abuse while allowing reasonable usage patterns.

### Edge Cases
- **Redis unavailable:** If Redis connection fails, the service falls back to database reads. Log a warning. Do not fail the request. Performance will degrade but functionality is preserved.
- **Cache and DB disagree:** Cache is always ephemeral (5s TTL). If a stale value is served, it will be refreshed within 5 seconds. For time-critical accuracy, use `force_refresh`.
- **Account with no balance record:** Return a zero balance with the tenant's primary currency. This should not happen for active accounts (STORY-013 creates the record) but handle defensively.
- **Newly activated account:** Balance record created in STORY-013 saga. `GetBalance` called immediately after activation may hit before the balance record is committed. Retry with a short delay or return zero.
- **Concurrent transactions:** Multiple `TransactionCompleted` events may fire simultaneously. Each invalidates the cache independently. No race condition because the cache is simply deleted (not updated).
- **Large number of accounts:** Redis memory usage is minimal -- each balance entry is ~200 bytes with a 5s TTL. Even 1 million concurrent cached balances would use ~200MB.
- **Clock skew:** `cache_timestamp` uses the application server's clock. Minor clock skew between servers is acceptable for a 5s TTL.

---

## Dependencies

**Prerequisite Stories:**
- STORY-013: Account Activation on KYC Approval (creates the balance record)

**Blocked Stories:**
- None directly, but balance display is a prerequisite for meaningful transaction features

**External Dependencies:**
- Redis instance provisioned and accessible from the application
- StackExchange.Redis NuGet package

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Cache hit/miss verified: first call reads from DB, subsequent calls (within 5s) from cache
- [ ] Cache invalidation verified: TransactionCompleted event clears the cache
- [ ] Force refresh verified: bypasses cache and reads from DB
- [ ] Redis fallback verified: service functions correctly when Redis is unavailable
- [ ] Performance verified: p95 response time < 500ms under load testing
- [ ] Balance precision verified: no floating point rounding errors

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
