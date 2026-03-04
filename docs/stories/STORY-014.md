# STORY-014: Device Binding on Registration

**Epic:** EPIC-001
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a new user
I want my account bound to my device
So that unauthorized devices cannot access my account

---

## Description

### Background
Device binding is a critical security layer for mobile banking in Southern Africa, where SIM-swap fraud and account takeover attacks are prevalent. By binding a user's account to a specific physical device during registration, UniBank ensures that even if credentials are compromised, an attacker cannot access the account from a different device without completing a re-verification process.

The device fingerprint consists of the Android ID (a unique per-device, per-app identifier) combined with the app installation ID (a UUID generated on first app launch). This combination ensures uniqueness while respecting user privacy. The device ID is embedded in the JWT token as a claim, and the API Gateway validates that every authenticated request originates from the bound device.

Device transfer is supported for legitimate use cases (new phone, factory reset) through a secure re-verification process involving OTP and PIN confirmation.

### Scope
**In scope:**
- Capture device fingerprint (Android ID + app installation ID) during registration
- Store device fingerprint in the `accounts` table
- Include `device_id` claim in JWT tokens
- API Gateway interceptor to validate device_id in token matches the request header
- Device change detection and blocking of requests from unbound devices
- Device transfer flow: OTP + PIN re-verification to bind a new device
- Audit logging of device binding and transfer events

**Out of scope:**
- iOS device binding (Android-first for the target market; iOS support in future sprint)
- Multi-device support (one account = one device for initial release)
- Device risk scoring or behavioral biometrics
- Remote device wipe capability
- Rooted/jailbroken device detection (future security enhancement)

### User Flow

**Initial Binding (during registration):**
1. User installs the UniBank app and launches it for the first time
2. App generates a device fingerprint: SHA-256(Android_ID + Installation_UUID)
3. During registration (STORY-009), the device fingerprint is sent as part of the registration request
4. Server stores the device fingerprint in the `accounts.device_id` column
5. All subsequent JWT tokens include the `device_id` claim
6. API Gateway validates `device_id` on every authenticated request

**Device Transfer (new device):**
1. User installs UniBank on a new device and attempts to log in
2. App sends login request with the new device's fingerprint
3. Server detects device_id mismatch and returns `DEVICE_MISMATCH` error
4. App prompts user: "This device is not registered. To transfer your account, verify your identity."
5. User initiates device transfer: enters phone number
6. Server sends OTP to registered phone number
7. User enters OTP
8. User enters their PIN
9. Server validates OTP + PIN, updates `device_id` to the new device fingerprint
10. Server invalidates all existing sessions and tokens
11. User receives a new JWT with the updated device_id claim
12. Old device can no longer access the account

---

## Acceptance Criteria

- [ ] Device fingerprint (Android ID + app installation ID hash) is captured during registration
- [ ] Device fingerprint is stored in the `accounts.device_id` column
- [ ] JWT access tokens include the `device_id` claim
- [ ] API Gateway interceptor validates that the `device_id` in the JWT matches the `X-Device-Id` header in the request
- [ ] Requests from a non-matching device are rejected with a `DEVICE_MISMATCH` error (gRPC status `PERMISSION_DENIED`)
- [ ] Device transfer is available via OTP + PIN re-verification
- [ ] Successful device transfer updates the `device_id` in the accounts table
- [ ] All existing sessions and tokens are invalidated after device transfer
- [ ] Device binding and transfer events are logged in the audit trail
- [ ] Device transfer sends a notification to the old device (if reachable) warning of account migration

---

## Technical Notes

### Components
- **AccountModule** (`src/Modules/Account/`):
  - `AccountService.cs`: Extended to handle device binding during registration
  - `DeviceBindingService.cs`: Device validation, transfer logic
  - `AccountRepository.cs`: Device ID storage and updates
- **AuthModule** (`src/Modules/Auth/`):
  - `TokenService.cs`: Include device_id claim in JWT generation
  - `DeviceTransferHandler.cs`: OTP + PIN verification for device change
- **ApiGateway** (`src/ApiGateway/`):
  - `DeviceValidationInterceptor.cs`: gRPC interceptor that validates device_id claim against request header
- **NotificationModule** (`src/Modules/Notification/`):
  - Send warning notification to old device on transfer

### API / gRPC Endpoints

**Service:** `AccountService`

```protobuf
service AccountService {
  // Existing registration endpoint enhanced with device_id
  rpc Register(RegisterRequest) returns (RegisterResponse);  // From STORY-009, add device_id field

  // Device transfer initiation
  rpc InitiateDeviceTransfer(InitiateDeviceTransferRequest) returns (InitiateDeviceTransferResponse);

  // Complete device transfer with OTP + PIN
  rpc CompleteDeviceTransfer(CompleteDeviceTransferRequest) returns (CompleteDeviceTransferResponse);
}

message InitiateDeviceTransferRequest {
  string phone_number = 1;
  string new_device_id = 2;
}

message InitiateDeviceTransferResponse {
  string transfer_reference = 1;
  string message = 2;                    // "OTP sent to your registered number"
  int32 otp_expiry_seconds = 3;
}

message CompleteDeviceTransferRequest {
  string transfer_reference = 1;
  string otp = 2;
  string pin = 3;
  string new_device_id = 4;
}

message CompleteDeviceTransferResponse {
  bool success = 1;
  string access_token = 2;              // New JWT with updated device_id
  string refresh_token = 3;
  string message = 4;
}
```

**Gateway Interceptor Logic:**

```csharp
public class DeviceValidationInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var deviceIdFromToken = context.GetHttpContext().User.FindFirst("device_id")?.Value;
        var deviceIdFromHeader = context.RequestHeaders.GetValue("x-device-id");

        if (deviceIdFromToken != deviceIdFromHeader)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "DEVICE_MISMATCH"));
        }

        return await continuation(request, context);
    }
}
```

### Database Changes

**Column Addition:** `accounts.device_id`

```sql
ALTER TABLE accounts ADD COLUMN device_id VARCHAR(64);
CREATE INDEX idx_accounts_device_id ON accounts(device_id);
```

**Table:** `device_transfer_requests` (schema: `{tenant_schema}`)

```sql
CREATE TABLE device_transfer_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    old_device_id VARCHAR(64) NOT NULL,
    new_device_id VARCHAR(64) NOT NULL,
    transfer_reference VARCHAR(50) NOT NULL UNIQUE,
    otp_hash VARCHAR(255) NOT NULL,
    otp_expires_at TIMESTAMPTZ NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_device_transfer_account FOREIGN KEY (account_id) REFERENCES accounts(id)
);

CREATE INDEX idx_device_transfer_reference ON device_transfer_requests(transfer_reference);
CREATE INDEX idx_device_transfer_account ON device_transfer_requests(account_id);
```

**Status Values:** `pending`, `completed`, `expired`, `failed`

### Security Considerations
- **Device Fingerprint Integrity:** The fingerprint is a SHA-256 hash of Android ID + Installation UUID, making it difficult to spoof. However, rooted devices could potentially manipulate Android ID; this is an accepted risk for the initial release.
- **JWT Device Claim:** The `device_id` claim is signed within the JWT, preventing tampering. The gateway validates the claim against the request header, ensuring the device sending the request matches the token.
- **Transfer Security:** Device transfer requires both OTP (proves phone possession) and PIN (proves account knowledge). This two-factor verification prevents unauthorized device transfers.
- **Session Invalidation:** All existing sessions and refresh tokens are invalidated on device transfer, preventing the old device from accessing the account.
- **Notification on Transfer:** The old device receives a push notification warning about the account migration, alerting the legitimate user if the transfer was unauthorized.
- **Rate Limiting:** Device transfer attempts limited to 3 per 24 hours per account to prevent brute-force OTP attacks.
- **Audit Trail:** All device-related events logged: initial binding, transfer attempts (success/failure), and mismatched device access attempts.

### Edge Cases
- **App reinstalled on same device:** Android ID persists across reinstalls, but Installation UUID changes. The combined fingerprint will differ. User must complete device transfer flow. Consider caching the installation UUID in Android Keystore for persistence.
- **Factory reset:** Both Android ID and Installation UUID change. Full device transfer required.
- **Device transfer OTP expires:** Transfer reference becomes invalid after OTP expiry (configurable, default 5 minutes). User must initiate a new transfer.
- **Multiple concurrent transfer requests:** Only the most recent transfer request is valid. Older pending requests are automatically expired.
- **Network failure during transfer completion:** Transfer is atomic; if the server processes the request, the response might be lost. Client should retry with the same transfer_reference; server handles idempotently.
- **User loses phone and SIM:** Cannot complete device transfer (no OTP delivery). Must contact support for manual identity verification and device reset. This is a known limitation for the initial release.

---

## Dependencies

**Prerequisite Stories:**
- STORY-009: User Registration (device binding happens during registration)

**Blocked Stories:**
- STORY-018: PIN & Biometric Authentication (device_id required in JWT)
- STORY-019: Session Management (device validation is part of session security)

**External Dependencies:**
- SMS gateway for OTP delivery (shared with registration)
- Firebase Cloud Messaging for old-device notification

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Device binding verified: registration stores device_id correctly
- [ ] JWT verified: device_id claim present in generated tokens
- [ ] Gateway interceptor verified: mismatched device_id returns PERMISSION_DENIED
- [ ] Device transfer verified end-to-end: OTP + PIN -> new device bound -> old sessions invalidated
- [ ] Audit logging verified for all device events

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
