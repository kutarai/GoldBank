# STORY-030: P2P Cross-Border Transfer

**Epic:** EPIC-005 P2P Transfers
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a consumer,
I want to send money to users in other Southern African countries,
So that I can support family cross-border.

---

## Description

### Background
Cross-border remittances are a critical use case for Southern Africa's unbanked population. Millions of migrant workers in countries like South Africa, Botswana, and Namibia regularly send money to family in Zimbabwe, Mozambique, Malawi, and other SADC countries. Traditional remittance channels (Western Union, MoneyGram) charge fees of 8-15%, while informal channels (bus drivers, cross-border traders) are risky and unreliable.

GoldBank's cross-border transfer capability allows users in one country deployment (tenant) to send money to users in another country deployment. This involves cross-tenant communication, foreign exchange conversion, and compliance with cross-border transfer limits. The transfer may route through the switching server for inter-bank settlement when the recipient is on a different financial institution, or directly between GoldBank tenants when both parties are on the platform.

Functional Requirement: **FR-016**.

### Scope

**In scope:**
- Destination country selection from supported SADC corridors
- Real-time exchange rate display (configurable source: manual or API)
- Cross-border daily and monthly transfer limit enforcement per tenant config
- Recipient lookup across tenants (by phone number + country code)
- Wolverine saga with FX conversion step: debit sender -> convert currency -> credit receiver -> record fees
- Saga compensation with FX reversal
- Compliance metadata capture (purpose of transfer, source of funds)

**Out of scope:**
- Transfers to non-GoldBank recipients in other countries (future: interoperability)
- Real-time exchange rate API integration (manual/admin-configured rates for MVP)
- Transfers to countries outside the supported SADC corridor list
- Cash pickup at destination (future: agent network integration across borders)
- Regulatory reporting automation (captured for manual review in MVP)

### User Flow
1. Consumer opens "Send Money" and selects "International Transfer"
2. Consumer selects destination country from supported list (e.g., Zimbabwe, Mozambique, Malawi)
3. Consumer enters recipient phone number (with country code) or selects from contacts
4. System looks up recipient across tenants and displays masked name + destination currency
5. Consumer enters amount in their local currency
6. System displays: amount in sender currency, exchange rate, amount in receiver currency, fees, total debit
7. Consumer selects purpose of transfer (family support, education, medical, business, other)
8. Consumer reviews confirmation screen and confirms
9. System executes cross-border Wolverine saga
10. Consumer sees success screen with reference number (XBR-{tenant}-{YYYYMMDD}-{seq})
11. Receiver receives notification in their local currency amount

---

## Acceptance Criteria

- [ ] Consumer can select a destination country from the list of supported SADC corridors
- [ ] System displays the current exchange rate for the selected corridor
- [ ] Consumer can enter a recipient phone number with the correct country code for the destination
- [ ] System validates recipient exists in the destination tenant and account is active
- [ ] Consumer can enter the transfer amount in their local (sender) currency
- [ ] System calculates and displays: sender amount, exchange rate, receiver amount, transfer fee, and total debit
- [ ] System enforces cross-border per-transaction limits from tenant configuration
- [ ] System enforces cross-border daily cumulative limits from tenant configuration
- [ ] System enforces cross-border monthly cumulative limits from tenant configuration
- [ ] Consumer must select a purpose of transfer from a predefined list
- [ ] Confirmation screen displays all details including exchange rate and converted amount
- [ ] Consumer can cancel from the confirmation screen without any funds being moved
- [ ] Wolverine saga executes: DebitSenderAccount -> ConvertCurrency -> CreditReceiverAccount -> RecordFees -> PublishTransferCompleted
- [ ] If currency conversion or credit fails, saga compensation reverses the sender debit
- [ ] Unique reference number generated in format XBR-{tenant}-{YYYYMMDD}-{seq}
- [ ] Both sender and receiver have transaction history entries with correct local currency amounts
- [ ] Receiver is notified with the amount in their local currency
- [ ] Compliance metadata (purpose, source country, destination country) is stored with the transfer record

---

## Technical Notes

### Components

**Module:** `GoldBank.Core/Modules/Transfers/`

```
Transfers/
  Domain/
    Entities/
      CrossBorderTransfer.cs        # Extends Transfer with FX details
      ExchangeRate.cs               # Exchange rate snapshot entity
    ValueObjects/
      CountryCode.cs                # ISO 3166-1 alpha-2 codes
      CurrencyCorridor.cs           # Source/destination currency pair
      TransferPurpose.cs            # Enum: FamilySupport, Education, Medical, Business, Other
    Events/
      CrossBorderTransferInitiated.cs
      CurrencyConverted.cs
      CrossBorderTransferCompleted.cs
      CrossBorderTransferFailed.cs
  Application/
    Commands/
      SendCrossBorderTransferCommand.cs
      SendCrossBorderTransferHandler.cs
    Queries/
      PreviewCrossBorderTransferQuery.cs
      GetExchangeRateQuery.cs
      GetSupportedCorridorsQuery.cs
      LookupCrossTenantRecipientQuery.cs
    Validators/
      SendCrossBorderTransferValidator.cs
    Sagas/
      CrossBorderTransferSaga.cs     # Extended saga with FX step
  Infrastructure/
    Services/
      ExchangeRateService.cs         # Rate lookup (config or API)
      CrossTenantTransferService.cs  # Cross-schema communication
      CrossBorderLimitService.cs     # Daily/monthly limit tracking
```

### API / gRPC Endpoints

**GetSupportedCorridors:**
```protobuf
rpc GetSupportedCorridors(GetCorridorsRequest) returns (GetCorridorsResponse);

message GetCorridorsRequest {}

message Corridor {
  string destination_country_code = 1;
  string destination_country_name = 2;
  string destination_currency = 3;
  string exchange_rate = 4;
  string min_amount = 5;
  string max_amount = 6;
}

message GetCorridorsResponse {
  repeated Corridor corridors = 1;
}
```

**PreviewCrossBorderTransfer:**
```protobuf
rpc PreviewCrossBorderTransfer(PreviewCrossBorderRequest) returns (PreviewCrossBorderResponse);

message PreviewCrossBorderRequest {
  string sender_account_id = 1;
  string recipient_phone = 2;
  string destination_country_code = 3;
  string amount = 4;
  string sender_currency = 5;
}

message PreviewCrossBorderResponse {
  string recipient_masked_name = 1;
  string sender_amount = 2;
  string sender_currency = 3;
  string exchange_rate = 4;
  string receiver_amount = 5;
  string receiver_currency = 6;
  string fee_amount = 7;
  string total_debit = 8;
  google.protobuf.Timestamp rate_valid_until = 9;
}
```

**SendCrossBorderTransfer:**
```protobuf
rpc SendCrossBorderTransfer(SendCrossBorderRequest) returns (SendCrossBorderResponse);

message SendCrossBorderRequest {
  string sender_account_id = 1;
  string recipient_phone = 2;
  string destination_country_code = 3;
  string amount = 4;
  string sender_currency = 5;
  TransferPurpose purpose = 6;
  string idempotency_key = 7;
}

enum TransferPurpose {
  FAMILY_SUPPORT = 0;
  EDUCATION = 1;
  MEDICAL = 2;
  BUSINESS = 3;
  OTHER = 4;
}

message SendCrossBorderResponse {
  bool success = 1;
  string reference_number = 2;
  string receiver_amount = 3;
  string receiver_currency = 4;
  string exchange_rate_applied = 5;
  string error_code = 6;
  string error_message = 7;
  google.protobuf.Timestamp completed_at = 8;
}
```

### Database Changes

**Table: `cross_border_transfers` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.cross_border_transfers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transfer_id UUID NOT NULL REFERENCES {tenant_schema}.transfers(id),
    destination_country_code VARCHAR(2) NOT NULL,
    destination_tenant_id UUID NOT NULL,
    sender_currency VARCHAR(3) NOT NULL,
    receiver_currency VARCHAR(3) NOT NULL,
    exchange_rate DECIMAL(18,8) NOT NULL,
    sender_amount DECIMAL(18,4) NOT NULL,
    receiver_amount DECIMAL(18,4) NOT NULL,
    transfer_purpose VARCHAR(30) NOT NULL,
    compliance_notes TEXT,
    rate_snapshot_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_xborder_transfer ON {tenant_schema}.cross_border_transfers(transfer_id);
CREATE INDEX idx_xborder_dest_country ON {tenant_schema}.cross_border_transfers(destination_country_code);
```

**Table: `exchange_rates` (public schema)**
```sql
CREATE TABLE public.exchange_rates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_currency VARCHAR(3) NOT NULL,
    destination_currency VARCHAR(3) NOT NULL,
    rate DECIMAL(18,8) NOT NULL,
    inverse_rate DECIMAL(18,8) NOT NULL,
    effective_from TIMESTAMPTZ NOT NULL,
    effective_until TIMESTAMPTZ,
    source VARCHAR(50) NOT NULL DEFAULT 'manual',
    created_by UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(source_currency, destination_currency, effective_from)
);

CREATE INDEX idx_exchange_rates_pair ON public.exchange_rates(source_currency, destination_currency);
CREATE INDEX idx_exchange_rates_effective ON public.exchange_rates(effective_from, effective_until);
```

**Table: `cross_border_limits` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.cross_border_limits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    destination_country_code VARCHAR(2),
    per_transaction_limit DECIMAL(18,4) NOT NULL,
    daily_limit DECIMAL(18,4) NOT NULL,
    monthly_limit DECIMAL(18,4) NOT NULL,
    currency VARCHAR(3) NOT NULL,
    kyc_tier VARCHAR(20) NOT NULL DEFAULT 'basic',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### Wolverine Saga: CrossBorderTransferSaga

```csharp
public class CrossBorderTransferSaga : Saga
{
    public Guid TransferId { get; set; }
    public Guid SenderAccountId { get; set; }
    public string RecipientPhone { get; set; }
    public Guid DestinationTenantId { get; set; }
    public decimal SenderAmount { get; set; }
    public decimal ReceiverAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public string SenderCurrency { get; set; }
    public string ReceiverCurrency { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool SenderDebited { get; set; }
    public bool CurrencyConverted { get; set; }

    // Step 1: Debit sender in sender's currency (amount + fee)
    public DebitAccount Handle(StartCrossBorderTransfer cmd) { ... }

    // Step 2: Convert currency (snapshot rate, calculate receiver amount)
    public ConvertCurrency Handle(AccountDebited evt) { ... }

    // Step 3: Credit receiver in destination tenant (receiver's currency)
    public CreditCrossTenantAccount Handle(CurrencyConverted evt) { ... }

    // Step 4: Record fees and complete
    public object[] Handle(CrossTenantAccountCredited evt)
    {
        MarkCompleted();
        return new object[]
        {
            new RecordTransactionFee(TransferId, FeeAmount, SenderCurrency),
            new CrossBorderTransferCompleted(TransferId, SenderAccountId, RecipientPhone,
                SenderAmount, ReceiverAmount, ExchangeRate)
        };
    }

    // Compensation: Reverse debit if downstream steps fail
    public ReverseDebit Handle(CrossTenantCreditFailed evt) { ... }
    public ReverseDebit Handle(CurrencyConversionFailed evt) { ... }
}
```

### Security Considerations
- **Cross-tenant access:** The cross-tenant credit operation must use a service-to-service authentication mechanism, not the user's JWT
- **Exchange rate locking:** Rate displayed at preview must be locked for a configurable window (default 60 seconds); if expired, re-fetch
- **Compliance:** Purpose of transfer and country corridor are mandatory for regulatory reporting
- **Sanctions screening:** Placeholder for future integration with sanctions lists (OFAC, UN, local)
- **Amount masking:** Cross-tenant recipient lookup returns only masked name; full details never cross tenant boundaries
- **Rate manipulation:** Exchange rates are admin-configured and audited; no user-facing rate negotiation

### Edge Cases
- **Exchange rate changes between preview and execution:** Validate rate is still within the locked window; reject if expired
- **Destination tenant unavailable:** Saga must handle timeout; mark transfer as "pending_external" and retry
- **Cross-tenant credit fails:** Full compensation — reverse sender debit at the original exchange rate (no FX loss to user)
- **Destination country limit reached:** Check daily/monthly cumulative before initiating saga
- **Recipient not found in destination tenant:** Clear error with suggestion to verify phone number and country
- **Same-currency corridor (e.g., ZAR to ZAR within SACU):** Exchange rate = 1.0, but still tracked as cross-border for compliance
- **Network partition between tenants:** Wolverine durability ensures saga resumes; potential for temporary inconsistency resolved on reconnection

---

## Dependencies

**Prerequisite Stories:**
- STORY-029: P2P Domestic Transfer (base transfer infrastructure and saga patterns)

**Blocked Stories:**
- No direct blockers in Sprint 4; future cross-border enhancements depend on this

**External Dependencies:**
- Exchange rate data source (manual admin entry for MVP; future API integration)
- Cross-tenant communication mechanism (internal gRPC or shared message bus)
- Switching server may be needed for inter-bank cross-border routing (STORY-048)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (cross-tenant saga happy path and compensation)
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
