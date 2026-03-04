# STORY-039: Saved/Favorite Billers

**Epic:** EPIC-007 Bill Payments
**Priority:** Should Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a user,
I want to save frequently used billers,
So that I can quickly pay recurring bills.

---

## Description

### Background
Many bill payments in Southern Africa are recurring — electricity top-ups, water bills, airtime purchases, and insurance premiums. Users pay the same provider with the same account number on a regular basis (weekly, monthly). Requiring users to re-enter provider selection and account numbers every time creates friction and increases the risk of input errors.

The saved/favorite billers feature allows users to store their commonly used biller configurations (provider + account number + optional nickname). When making a subsequent payment, users can select from their saved billers list, which pre-fills the provider and account number — the user only needs to enter the amount and confirm. This significantly reduces payment time and errors, which is especially important for users with limited digital literacy.

Functional Requirement: **FR-027**.

### Scope

**In scope:**
- Save a biller after a successful bill payment (prompt) or manually from the billers screen
- Saved biller data: provider reference, account number, optional user-assigned nickname
- List saved billers with provider details (name, category, icon reference)
- Initiate a bill payment from a saved biller (pre-fills provider and account number)
- Remove a saved biller
- Edit saved biller nickname
- Maximum saved billers per account (configurable, default 20)

**Out of scope:**
- Automatic/scheduled payments from saved billers (future: standing orders)
- Bill amount prediction based on payment history
- Biller recommendation engine
- Sharing saved billers between accounts
- Importing billers from other platforms

### User Flow

**Save a Biller (Post-Payment):**
1. User completes a bill payment successfully (STORY-038)
2. System prompts: "Save this biller for quick payments? [Yes] [No]"
3. User taps "Yes"
4. System optionally prompts for a nickname (e.g., "Home Electricity")
5. Biller is saved with provider + account number + nickname

**Save a Biller (Manual):**
1. User navigates to "Saved Billers" in the app
2. User taps "Add Biller"
3. User selects a provider from the registry
4. User enters the account/reference number
5. System validates the account format
6. User enters an optional nickname
7. Biller is saved

**Pay from Saved Biller:**
1. User opens "Pay Bills" or "Saved Billers"
2. User sees their list of saved billers with nicknames and provider names
3. User selects a saved biller
4. System pre-fills provider and account number
5. User enters the payment amount
6. Normal bill payment flow continues (STORY-038) from the amount entry step

**Remove a Saved Biller:**
1. User opens "Saved Billers"
2. User swipes or selects a biller and taps "Remove"
3. System confirms: "Remove {nickname}? This won't affect past payments."
4. User confirms; biller is removed from saved list

---

## Acceptance Criteria

- [ ] User can save a biller with: provider ID, account/reference number, and optional nickname
- [ ] System prompts user to save a biller after a successful bill payment
- [ ] User can manually add a saved biller by selecting a provider and entering an account number
- [ ] Account number is validated against the provider's format regex before saving
- [ ] User can assign and later edit a nickname for each saved biller (e.g., "Home Electricity", "Mom's Airtime")
- [ ] If no nickname is provided, the system generates a default: "{Provider Name} - {masked account number}"
- [ ] User can retrieve their list of saved billers sorted by most recently used
- [ ] Saved biller list includes: nickname, provider name, provider category, masked account number
- [ ] User can initiate a bill payment from a saved biller; provider and account number are pre-filled
- [ ] Pre-filled payment flow skips provider selection and account entry, starting at amount entry
- [ ] User can remove a saved biller; removal does not affect past payment records
- [ ] System enforces a maximum number of saved billers per account (configurable, default 20)
- [ ] If maximum reached, user is prompted to remove an existing biller before adding a new one
- [ ] Duplicate detection: system prevents saving the same provider + account number combination twice for the same account
- [ ] If a provider is deactivated, saved billers referencing it are marked as unavailable (not deleted)

---

## Technical Notes

### Components

**Module:** `UniBank.Core/Modules/BillPay/`

```
BillPay/
  Domain/
    Entities/
      SavedBiller.cs                  # Saved biller entity
    ValueObjects/
      BillerNickname.cs               # Validated nickname (max 50 chars)
  Application/
    Commands/
      SaveBillerCommand.cs            # Save a new biller
      SaveBillerHandler.cs
      UpdateBillerNicknameCommand.cs  # Edit nickname
      UpdateBillerNicknameHandler.cs
      RemoveSavedBillerCommand.cs     # Remove a saved biller
      RemoveSavedBillerHandler.cs
    Queries/
      GetSavedBillersQuery.cs         # List all saved billers for account
      GetSavedBillersHandler.cs
    Validators/
      SaveBillerValidator.cs          # Validates provider exists, account format, max count
  Infrastructure/
    Persistence/
      SavedBillerRepository.cs
      SavedBillerEntityConfiguration.cs
    Services/
      SavedBillerService.cs           # BillPayService.SaveBiller/GetSavedBillers/RemoveSavedBiller
  Grpc/
    BillPayGrpcService.cs             # Extended with saved biller endpoints
```

### API / gRPC Endpoints

**SaveBiller:**
```protobuf
rpc SaveBiller(SaveBillerRequest) returns (SaveBillerResponse);

message SaveBillerRequest {
  string account_id = 1;
  string provider_id = 2;
  string account_number = 3;
  string nickname = 4;               // optional
}

message SaveBillerResponse {
  bool success = 1;
  string saved_biller_id = 2;
  string error_code = 3;
  string error_message = 4;
}
```

**GetSavedBillers:**
```protobuf
rpc GetSavedBillers(GetSavedBillersRequest) returns (GetSavedBillersResponse);

message GetSavedBillersRequest {
  string account_id = 1;
}

message SavedBillerInfo {
  string id = 1;
  string provider_id = 2;
  string provider_name = 3;
  string provider_category = 4;
  string account_number_masked = 5;  // ****1234
  string nickname = 6;
  bool provider_available = 7;       // false if provider deactivated
  google.protobuf.Timestamp last_used = 8;
  google.protobuf.Timestamp created_at = 9;
}

message GetSavedBillersResponse {
  repeated SavedBillerInfo billers = 1;
  int32 max_billers = 2;             // configured maximum
  int32 current_count = 3;
}
```

**UpdateBillerNickname:**
```protobuf
rpc UpdateBillerNickname(UpdateBillerNicknameRequest) returns (UpdateBillerNicknameResponse);

message UpdateBillerNicknameRequest {
  string saved_biller_id = 1;
  string account_id = 2;
  string new_nickname = 3;
}

message UpdateBillerNicknameResponse {
  bool success = 1;
  string error_code = 2;
  string error_message = 3;
}
```

**RemoveSavedBiller:**
```protobuf
rpc RemoveSavedBiller(RemoveSavedBillerRequest) returns (RemoveSavedBillerResponse);

message RemoveSavedBillerRequest {
  string saved_biller_id = 1;
  string account_id = 2;
}

message RemoveSavedBillerResponse {
  bool success = 1;
  string error_code = 2;
  string error_message = 3;
}
```

### Database Changes

**Table: `saved_billers` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.saved_billers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES {tenant_schema}.accounts(id),
    provider_id UUID NOT NULL REFERENCES public.bill_providers(id),
    account_number VARCHAR(100) NOT NULL,
    nickname VARCHAR(50),
    last_used_at TIMESTAMPTZ,
    use_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(account_id, provider_id, account_number)
);

CREATE INDEX idx_saved_billers_account ON {tenant_schema}.saved_billers(account_id);
CREATE INDEX idx_saved_billers_last_used ON {tenant_schema}.saved_billers(account_id, last_used_at DESC NULLS LAST);
```

### Integration with Bill Payment Flow

```csharp
// After successful bill payment, prompt to save
public class BillPaymentCompletedHandler
{
    public async Task Handle(BillPaymentCompleted evt, ISavedBillerService savedBillerService)
    {
        // Check if this biller is already saved
        var exists = await savedBillerService.ExistsAsync(
            evt.AccountId, evt.ProviderId, evt.AccountNumber);

        if (!exists)
        {
            // Publish event for mobile app to prompt user
            await _messageBus.PublishAsync(new PromptSaveBiller
            {
                AccountId = evt.AccountId,
                ProviderId = evt.ProviderId,
                ProviderName = evt.ProviderName,
                AccountNumber = evt.AccountNumber
            });
        }
        else
        {
            // Update last_used_at and use_count
            await savedBillerService.RecordUsageAsync(evt.AccountId, evt.ProviderId, evt.AccountNumber);
        }
    }
}

// Payment from saved biller: pre-fill and skip to amount entry
public class PayFromSavedBillerHandler
{
    public async Task<PreviewBillPaymentResponse> Handle(PayFromSavedBillerQuery query)
    {
        var savedBiller = await _savedBillerRepo.GetByIdAsync(query.SavedBillerId);
        if (savedBiller == null)
            return Result.Failure("Saved biller not found");

        // Check provider is still active
        var provider = await _providerRepo.GetByIdAsync(savedBiller.ProviderId);
        if (provider.Status != "active")
            return Result.Failure("This provider is currently unavailable");

        // Return pre-filled preview
        return new PreviewBillPaymentResponse
        {
            ProviderId = savedBiller.ProviderId,
            ProviderName = provider.DisplayName,
            AccountNumber = savedBiller.AccountNumber,
            // Amount left for user to fill
        };
    }
}
```

### Nickname Generation

```csharp
public class NicknameGenerator
{
    public string GenerateDefault(string providerName, string accountNumber)
    {
        var maskedAccount = MaskAccountNumber(accountNumber);
        return $"{providerName} - {maskedAccount}";
    }

    private string MaskAccountNumber(string accountNumber)
    {
        if (accountNumber.Length <= 4)
            return new string('*', accountNumber.Length);

        return new string('*', accountNumber.Length - 4) + accountNumber[^4..];
    }
}
```

### Security Considerations
- **Account ownership:** Users can only save, view, and remove billers for their own accounts
- **Account number storage:** Full account number is stored (needed for payment pre-fill), but displayed masked in list views
- **No sensitive provider data exposed:** Saved biller list does not include API endpoints or internal provider configuration
- **Deletion is soft-optional:** Removing a saved biller is a hard delete (no soft delete needed — it contains no financial transaction data)
- **Nickname sanitization:** Nicknames are sanitized for XSS/injection; max 50 characters; alphanumeric + basic punctuation only

### Edge Cases
- **Provider deactivated after biller saved:** Saved biller remains but is marked `provider_available = false` in the response; attempting to pay shows "Provider currently unavailable"
- **Provider account format changes:** Existing saved billers with old format remain valid; new saves use new format regex
- **Maximum billers reached:** Clear error message with current count and maximum; user must remove one before adding
- **Duplicate save attempt:** Unique constraint on (account_id, provider_id, account_number); return existing saved biller ID with success
- **Save biller with empty nickname:** System generates default nickname from provider name + masked account
- **Account number contains special characters:** Stored as-is (some providers use alphanumeric references); displayed masked
- **User pays a saved biller that was recently removed:** Payment still works (uses standard flow); just won't be pre-filled
- **Concurrent save and remove:** Database constraints handle atomicity; no data corruption possible

---

## Dependencies

**Prerequisite Stories:**
- STORY-038: Pay Bill (saved billers reference providers and integrate with payment flow)

**Blocked Stories:**
- None; this is a terminal enhancement story in the bill payment epic

**External Dependencies:**
- None; saved billers are entirely internal data

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (save, list, pay-from-saved, remove, duplicate detection)
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
