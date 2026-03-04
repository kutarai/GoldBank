# STORY-038: Pay Bill

**Epic:** EPIC-007 Bill Payments
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a user,
I want to pay a bill by selecting provider and entering details,
So that I can pay utilities digitally.

---

## Description

### Background
Bill payments represent one of the most compelling reasons for unbanked users in Southern Africa to adopt digital financial services. Standing in long queues at utility offices, traveling to payment centers, or relying on informal agents to pay bills costs time and money. UniBank's digital bill payment capability allows users to pay electricity, water, telecom, internet, insurance, and government fees directly from their mobile phone.

The bill payment flow involves provider selection (from the registry in STORY-037), account/reference number entry with validation, amount entry within provider limits, PIN authorization for amounts above a configurable threshold, and execution via a Wolverine saga that debits the user's account and routes the payment to the provider's API. Provider integration uses a configurable API adapter pattern, allowing each provider to have a custom integration while sharing common validation and orchestration logic.

Functional Requirements: **FR-025** (Bill Payment), **FR-026** (Provider Integration).

### Scope

**In scope:**
- Provider selection from registry (STORY-037)
- Account/reference number entry with regex validation against provider format
- Amount entry with min/max validation against provider limits
- Confirmation screen with provider name, account number, amount, and fee
- PIN authorization for amounts above a configurable threshold
- Wolverine saga: DebitAccount -> RouteToProvider -> ConfirmPayment -> PublishTransactionCompleted
- Provider API adapter pattern for configurable integrations
- Timeout handling: if provider doesn't respond, mark as pending and retry
- Receipt with provider reference number
- Bill payment transaction history entries

**Out of scope:**
- Provider API development/hosting (external providers)
- Bill payment scheduling (pay future-dated bills)
- Partial payments with balance tracking
- Bill presentment (provider pushes bill to user)
- Direct debit / standing order setup
- Provider-specific UI customization

### User Flow
1. User opens "Pay Bills" and browses/searches providers (STORY-037)
2. User selects a provider (e.g., "ZESA Holdings - Electricity")
3. User enters their account/reference number (e.g., meter number)
4. System validates the format against the provider's regex pattern
5. User enters the payment amount
6. System validates amount is within provider's min/max range and user has sufficient balance
7. System displays confirmation screen: provider name, account number, amount, fee, total debit
8. If amount exceeds PIN threshold (tenant-configurable, e.g., $50), user must enter PIN
9. User confirms payment
10. System executes Wolverine saga: DebitAccount -> RouteToProvider -> ConfirmPayment -> PublishTransactionCompleted
11. On success, user sees receipt with UniBank reference number and provider reference number
12. Transaction appears in user's payment history

---

## Acceptance Criteria

- [ ] User can select a bill provider from the registry
- [ ] User can enter an account/reference number; system validates format against provider's regex
- [ ] System displays a clear error when account format is invalid, with the expected format hint
- [ ] User can enter a payment amount; system validates against provider min/max limits
- [ ] System displays a clear error when amount is below minimum or above maximum
- [ ] System validates user has sufficient balance (amount + fee) before showing confirmation
- [ ] Confirmation screen displays: provider name, account/reference number, amount, fee, total debit from account
- [ ] PIN is required when payment amount exceeds the tenant-configured threshold (default configurable per tenant)
- [ ] PIN is validated against the user's stored PIN hash (FR-061)
- [ ] Upon confirmation, Wolverine saga executes: DebitAccount -> RouteToProvider -> ConfirmPayment -> PublishTransactionCompleted
- [ ] If provider API responds with success, transaction is marked completed with provider reference number
- [ ] If provider API times out (configurable, default 30 seconds), transaction is marked as `pending` and queued for retry
- [ ] Retry policy: exponential backoff with max 3 retries over 15 minutes
- [ ] If all retries fail, transaction is marked as `failed` and user account is re-credited (compensation)
- [ ] If provider API responds with an error (invalid account, service unavailable), the saga compensates immediately
- [ ] Unique reference number generated: BIL-{tenant}-{YYYYMMDD}-{seq}
- [ ] Provider reference number (from provider API response) is stored and displayed on receipt
- [ ] User receives push notification upon successful bill payment
- [ ] Transaction appears in user's payment history with provider name, amount, and both reference numbers
- [ ] Fee is calculated per tenant fee schedule for bill payments

---

## Technical Notes

### Components

**Module:** `UniBank.Core/Modules/BillPay/`

```
BillPay/
  Domain/
    Entities/
      BillPayment.cs                  # Bill payment aggregate root
      BillPaymentStatus.cs            # Enum: Pending, Processing, Completed, Failed, Refunded
    ValueObjects/
      BillPaymentReference.cs         # BIL-{tenant}-{YYYYMMDD}-{seq}
      ProviderAccountNumber.cs        # Validated account number
    Events/
      BillPaymentInitiated.cs
      BillPaymentCompleted.cs
      BillPaymentFailed.cs
      BillPaymentPending.cs
  Application/
    Commands/
      PayBillCommand.cs               # Input: account_id, provider_id, account_number, amount
      PayBillHandler.cs
      RetryBillPaymentCommand.cs      # Retry pending payments
    Queries/
      PreviewBillPaymentQuery.cs      # Returns fee calculation, validation
      GetBillPaymentStatusQuery.cs    # Check status of pending payment
    Validators/
      PayBillValidator.cs             # FluentValidation: amount, account format
    Sagas/
      BillPaymentSaga.cs             # Wolverine saga
  Infrastructure/
    Providers/
      IBillProviderAdapter.cs         # Provider adapter interface
      GenericBillProviderAdapter.cs   # Default REST API adapter
      ZesaBillProviderAdapter.cs      # ZESA-specific adapter
      EskomBillProviderAdapter.cs     # Eskom-specific adapter
      EconetBillProviderAdapter.cs    # Econet airtime adapter
      BillProviderAdapterFactory.cs   # Factory to resolve adapter by type
    Services/
      BillPayService.cs               # BillPayService.PayBill
      BillPayRetryService.cs          # Retry pending payments
    Persistence/
      BillPaymentRepository.cs
      BillPaymentEntityConfiguration.cs
  Grpc/
    BillPayGrpcService.cs
```

### API / gRPC Endpoints

**PreviewBillPayment:**
```protobuf
rpc PreviewBillPayment(PreviewBillPaymentRequest) returns (PreviewBillPaymentResponse);

message PreviewBillPaymentRequest {
  string account_id = 1;
  string provider_id = 2;
  string account_number = 3;        // customer's account at the provider
  string amount = 4;
  string currency = 5;
}

message PreviewBillPaymentResponse {
  bool valid = 1;
  string provider_name = 2;
  string account_number = 3;
  string amount = 4;
  string fee = 5;
  string total_debit = 6;
  string currency = 7;
  bool pin_required = 8;
  string error_code = 9;
  string error_message = 10;
}
```

**PayBill:**
```protobuf
rpc PayBill(PayBillRequest) returns (PayBillResponse);

message PayBillRequest {
  string account_id = 1;
  string provider_id = 2;
  string account_number = 3;
  string amount = 4;
  string currency = 5;
  string pin = 6;                    // required if amount > threshold
  string idempotency_key = 7;
}

message PayBillResponse {
  bool success = 1;
  string reference_number = 2;       // BIL-{tenant}-{YYYYMMDD}-{seq}
  string provider_reference = 3;     // reference from provider API
  string status = 4;                 // 'completed' or 'pending' (if provider timeout)
  string error_code = 5;
  string error_message = 6;
  google.protobuf.Timestamp completed_at = 7;
}
```

**GetBillPaymentStatus:**
```protobuf
rpc GetBillPaymentStatus(GetBillPaymentStatusRequest) returns (GetBillPaymentStatusResponse);

message GetBillPaymentStatusRequest {
  string reference_number = 1;
}

message GetBillPaymentStatusResponse {
  string reference_number = 1;
  string provider_name = 2;
  string account_number = 3;
  string amount = 4;
  string status = 5;                 // pending, completed, failed
  string provider_reference = 6;
  string failure_reason = 7;
  google.protobuf.Timestamp last_updated = 8;
}
```

### Database Changes

**Table: `bill_payments` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.bill_payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference_number VARCHAR(50) NOT NULL UNIQUE,
    account_id UUID NOT NULL REFERENCES {tenant_schema}.accounts(id),
    provider_id UUID NOT NULL REFERENCES public.bill_providers(id),
    provider_name VARCHAR(150) NOT NULL,
    account_number VARCHAR(100) NOT NULL,            -- customer's account at the provider
    amount DECIMAL(18,4) NOT NULL,
    fee_amount DECIMAL(18,4) NOT NULL DEFAULT 0,
    currency VARCHAR(3) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',   -- pending, processing, completed, failed, refunded
    provider_reference VARCHAR(100),                 -- reference returned by provider
    provider_response JSONB,                         -- full provider API response
    retry_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3,
    next_retry_at TIMESTAMPTZ,
    initiated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    failed_at TIMESTAMPTZ,
    failure_reason TEXT,
    idempotency_key UUID NOT NULL UNIQUE,
    saga_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_bill_payments_account ON {tenant_schema}.bill_payments(account_id);
CREATE INDEX idx_bill_payments_reference ON {tenant_schema}.bill_payments(reference_number);
CREATE INDEX idx_bill_payments_status ON {tenant_schema}.bill_payments(status);
CREATE INDEX idx_bill_payments_retry ON {tenant_schema}.bill_payments(status, next_retry_at)
    WHERE status = 'pending' AND next_retry_at IS NOT NULL;
CREATE INDEX idx_bill_payments_idempotency ON {tenant_schema}.bill_payments(idempotency_key);
```

### Wolverine Saga: BillPaymentSaga

```csharp
public class BillPaymentSaga : Saga
{
    public Guid PaymentId { get; set; }
    public Guid AccountId { get; set; }
    public Guid ProviderId { get; set; }
    public string AccountNumber { get; set; }
    public decimal Amount { get; set; }
    public decimal FeeAmount { get; set; }
    public string Currency { get; set; }
    public string ProviderAdapterType { get; set; }
    public bool AccountDebited { get; set; }
    public bool ProviderPaid { get; set; }

    // Step 1: Debit user account (amount + fee)
    public DebitAccount Handle(StartBillPayment cmd)
    {
        PaymentId = cmd.PaymentId;
        AccountId = cmd.AccountId;
        ProviderId = cmd.ProviderId;
        AccountNumber = cmd.AccountNumber;
        Amount = cmd.Amount;
        FeeAmount = cmd.FeeAmount;
        Currency = cmd.Currency;
        ProviderAdapterType = cmd.ProviderAdapterType;

        return new DebitAccount(AccountId, Amount + FeeAmount, PaymentId);
    }

    // Step 2: Route payment to provider API
    public RouteToProvider Handle(AccountDebited evt)
    {
        AccountDebited = true;
        return new RouteToProvider(
            PaymentId, ProviderId, ProviderAdapterType,
            AccountNumber, Amount, Currency);
    }

    // Step 3: Confirm payment (provider returned success)
    public object[] Handle(ProviderPaymentSucceeded evt)
    {
        ProviderPaid = true;
        MarkCompleted();
        return new object[]
        {
            new ConfirmBillPayment(PaymentId, evt.ProviderReference),
            new BillPaymentCompleted(PaymentId, AccountId, ProviderId, Amount, evt.ProviderReference)
        };
    }

    // Provider timeout: mark pending for retry (don't compensate yet)
    public MarkPaymentPending Handle(ProviderPaymentTimedOut evt)
    {
        // Don't mark saga completed yet — retry will resume it
        return new MarkPaymentPending(PaymentId, evt.NextRetryAt);
    }

    // Provider error: compensate
    public ReverseDebit Handle(ProviderPaymentFailed evt)
    {
        if (AccountDebited)
        {
            return new ReverseDebit(AccountId, Amount + FeeAmount, PaymentId);
        }
        MarkCompleted();
        return null;
    }

    // All retries exhausted: compensate
    public ReverseDebit Handle(BillPaymentRetriesExhausted evt)
    {
        if (AccountDebited && !ProviderPaid)
        {
            MarkCompleted();
            return new ReverseDebit(AccountId, Amount + FeeAmount, PaymentId);
        }
        MarkCompleted();
        return null;
    }
}
```

### Provider Adapter Pattern

```csharp
public interface IBillProviderAdapter
{
    Task<ProviderPaymentResult> PayAsync(ProviderPaymentRequest request, CancellationToken ct);
    Task<ProviderStatusResult> CheckStatusAsync(string providerReference, CancellationToken ct);
}

public class ProviderPaymentRequest
{
    public string AccountNumber { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string InternalReference { get; set; }
}

public class ProviderPaymentResult
{
    public bool Success { get; set; }
    public string ProviderReference { get; set; }
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
    public bool IsTimeout { get; set; }
}

// Generic REST API adapter
public class GenericBillProviderAdapter : IBillProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly string _apiEndpoint;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public async Task<ProviderPaymentResult> PayAsync(ProviderPaymentRequest request, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            var response = await _httpClient.PostAsJsonAsync(_apiEndpoint, new
            {
                account = request.AccountNumber,
                amount = request.Amount,
                currency = request.Currency,
                reference = request.InternalReference
            }, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ProviderApiResponse>(cts.Token);
                return new ProviderPaymentResult
                {
                    Success = true,
                    ProviderReference = result.Reference
                };
            }

            return new ProviderPaymentResult
            {
                Success = false,
                ErrorCode = response.StatusCode.ToString(),
                ErrorMessage = await response.Content.ReadAsStringAsync(cts.Token)
            };
        }
        catch (OperationCanceledException)
        {
            return new ProviderPaymentResult { Success = false, IsTimeout = true };
        }
    }
}

// Factory resolves adapter by type
public class BillProviderAdapterFactory
{
    public IBillProviderAdapter Create(string adapterType, string apiEndpoint)
    {
        return adapterType switch
        {
            "zesa" => new ZesaBillProviderAdapter(apiEndpoint),
            "eskom" => new EskomBillProviderAdapter(apiEndpoint),
            "econet" => new EconetBillProviderAdapter(apiEndpoint),
            "mtn" => new MtnBillProviderAdapter(apiEndpoint),
            _ => new GenericBillProviderAdapter(apiEndpoint)
        };
    }
}
```

### Retry Service

```csharp
public class BillPayRetryService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pendingPayments = await _repository.GetPendingForRetryAsync(DateTime.UtcNow);

            foreach (var payment in pendingPayments)
            {
                if (payment.RetryCount >= payment.MaxRetries)
                {
                    await _messageBus.PublishAsync(new BillPaymentRetriesExhausted(payment.Id));
                    continue;
                }

                var adapter = _adapterFactory.Create(payment.ProviderAdapterType, payment.ApiEndpoint);
                var result = await adapter.PayAsync(new ProviderPaymentRequest
                {
                    AccountNumber = payment.AccountNumber,
                    Amount = payment.Amount,
                    Currency = payment.Currency,
                    InternalReference = payment.ReferenceNumber
                }, stoppingToken);

                if (result.Success)
                {
                    await _messageBus.PublishAsync(new ProviderPaymentSucceeded(payment.Id, result.ProviderReference));
                }
                else if (result.IsTimeout)
                {
                    payment.RetryCount++;
                    payment.NextRetryAt = DateTime.UtcNow.Add(GetBackoffDelay(payment.RetryCount));
                    await _repository.UpdateAsync(payment);
                }
                else
                {
                    await _messageBus.PublishAsync(new ProviderPaymentFailed(payment.Id, result.ErrorMessage));
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private TimeSpan GetBackoffDelay(int retryCount) =>
        TimeSpan.FromMinutes(Math.Pow(2, retryCount)); // 2, 4, 8 minutes
}
```

### Security Considerations
- **PIN authorization:** Required for amounts above tenant-configured threshold; prevents unauthorized bill payments
- **Provider API credentials:** Stored encrypted in configuration; never exposed in logs or responses
- **Account number validation:** Regex validation prevents injection attacks against provider APIs
- **Provider response sanitization:** Provider API responses are stored as JSONB but sanitized before display
- **Idempotency:** Prevents duplicate payments on retry (same idempotency_key returns cached result)
- **Amount limits:** Enforced at application and database level; prevents exceeding provider max or account limits

### Edge Cases
- **Provider API down at payment time:** Debit succeeds but provider times out; transaction marked pending; retried with exponential backoff
- **Provider accepts payment but API response lost:** Payment status check API used on retry to confirm status before re-submitting
- **Provider returns ambiguous error:** Treated as failure; compensation refunds user; provider-side reconciliation handles actual payment
- **User pays with exactly their remaining balance:** Balance check includes fee; if balance < amount + fee, reject before saga
- **Provider account number valid format but non-existent account:** Provider API returns error; saga compensates (refund)
- **Concurrent bill payments from same account:** Balance check with pessimistic locking prevents double-spend
- **Provider changes API format:** Adapter pattern isolates changes; only the specific adapter needs updating
- **Payment succeeds but notification fails:** Payment is still complete; notification retried independently
- **Extremely large bill payment (above daily limit):** Pre-validation against daily cumulative limit before saga start

---

## Dependencies

**Prerequisite Stories:**
- STORY-037: Bill Provider Registry (provider catalog and selection)
- STORY-020: PIN Management & Transaction Authorization (PIN verification for high-value payments)

**Blocked Stories:**
- STORY-039: Saved/Favorite Billers (saves provider + account for repeat payments)

**External Dependencies:**
- Bill provider APIs (ZESA, Eskom, Econet, etc.) must be accessible
- Provider API credentials must be configured per tenant
- HTTP client infrastructure for external API calls

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (saga happy path, timeout/retry, compensation)
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
