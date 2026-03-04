# STORY-018: PIN & Biometric Authentication

**Epic:** EPIC-014
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a user
I want to log in with PIN or biometric
So that my account is secured conveniently

---

## Description

### Background
Authentication is the gateway to every banking interaction. UniBank must balance security with convenience, especially for users in Southern Africa who may be using smartphones for the first time. The dual authentication model -- PIN and biometric -- provides this balance: PIN offers universal accessibility (works on all devices), while biometric (fingerprint or face unlock) provides a faster, more convenient experience for devices that support it.

The PIN is a 4-6 digit numeric code set during registration (STORY-010). It is stored as a bcrypt hash and validated server-side. Biometric authentication is handled device-side: when the user authenticates via fingerprint or face unlock on their device, the device's secure keystore releases the stored credentials (encrypted PIN or a device-specific authentication token), which are then sent to the server for validation.

The JWT token architecture uses short-lived access tokens (15 minutes) paired with long-lived refresh tokens (30 days) with rotation. This limits the window of exposure for a compromised access token while minimizing the frequency of full re-authentication. Account lockout after 5 failed attempts (30-minute lockout) protects against brute-force attacks.

### Scope
**In scope:**
- PIN-based authentication: validate PIN against bcrypt hash
- Biometric authentication: device-side biometric releases stored credentials
- JWT access token generation (15-minute expiry)
- JWT refresh token generation (30-day expiry) with rotation
- Access token contains: `sub` (account_id), `tenant_id`, `device_id`, `roles`
- Refresh token stored hashed in database
- Account lockout: 5 failed attempts triggers 30-minute lockout
- Lockout counter tracked in Redis (reset on successful login)
- Refresh token rotation: old token invalidated when used
- Logout endpoint to invalidate all tokens

**Out of scope:**
- PIN change flow (separate story)
- Biometric enrollment flow (handled in device settings, not server-side)
- Social login or third-party identity providers
- Multi-factor authentication beyond PIN + device binding
- Password-based authentication (UniBank uses PIN only)

### User Flow

**PIN Authentication:**
1. User opens the app and sees the login screen
2. User enters their phone number and 4-6 digit PIN
3. App sends `AccountService.Authenticate` request with phone_number, PIN, and device_id
4. Server validates device_id matches the bound device (STORY-014)
5. Server checks lockout status in Redis (`lockout:{tenant_id}:{account_id}`)
6. If locked out, return error with remaining lockout time
7. Server retrieves the bcrypt hash for the account
8. Server validates PIN against bcrypt hash
9. If PIN invalid: increment Redis failure counter; if counter reaches 5, set lockout TTL (30 min)
10. If PIN valid: reset failure counter, generate JWT access token (15 min) and refresh token (30 days)
11. Store hashed refresh token in database
12. Return tokens to client

**Biometric Authentication:**
1. User opens the app and sees biometric prompt (fingerprint icon or face scan)
2. User authenticates via device biometric (fingerprint/face)
3. Device keystore releases stored credentials (encrypted PIN or auth token)
4. App sends `AccountService.Authenticate` with biometric flag and released credentials
5. Server validates the credentials identically to PIN flow
6. Return JWT tokens

**Token Refresh:**
1. Access token expires (after 15 minutes)
2. App detects 401/UNAUTHENTICATED response
3. App sends `AccountService.RefreshToken` with the current refresh token
4. Server validates refresh token: matches hashed value in DB, not expired, not revoked
5. Server invalidates the old refresh token (rotation)
6. Server generates new access token and new refresh token
7. Returns new token pair to client
8. If refresh token is invalid/expired, user must re-authenticate with PIN/biometric

---

## Acceptance Criteria

- [ ] PIN authentication validates the entered PIN against the stored bcrypt hash
- [ ] Biometric authentication retrieves credentials from device keystore and validates server-side
- [ ] Successful authentication returns a JWT access token (15-minute expiry) and refresh token (30-day expiry)
- [ ] Access token contains claims: `sub` (account_id), `tenant_id`, `device_id`, `roles`
- [ ] Refresh token is stored hashed (SHA-256) in the database
- [ ] 5 consecutive failed authentication attempts trigger a 30-minute account lockout
- [ ] Lockout counter is tracked in Redis and reset on successful authentication
- [ ] Locked-out user receives a clear error with remaining lockout duration
- [ ] Refresh token rotation: using a refresh token invalidates it and issues a new one
- [ ] Using an already-invalidated refresh token (replay attack) invalidates ALL tokens for the account (token family revocation)
- [ ] Logout endpoint invalidates all active tokens for the account
- [ ] Authentication is rejected if the device_id does not match the bound device

---

## Technical Notes

### Components
- **AuthModule** (`src/Modules/Auth/`):
  - `AuthenticationService.cs`: PIN validation, lockout management, token generation
  - `TokenService.cs`: JWT generation, refresh token rotation, token revocation
  - `LockoutService.cs`: Redis-based failure counting and lockout management
- **AccountModule** (`src/Modules/Account/`):
  - `AccountService.cs`: Add `Authenticate`, `RefreshToken`, `Logout` gRPC methods
  - `AccountRepository.cs`: Credential retrieval
- **Infrastructure** (`src/Infrastructure/`):
  - `JwtTokenGenerator.cs`: JWT creation with proper claims
  - `BcryptHasher.cs`: PIN hashing and validation
  - `RedisLockoutStore.cs`: Redis operations for lockout tracking

### API / gRPC Endpoints

**Service:** `AccountService`

```protobuf
service AccountService {
  rpc Authenticate(AuthenticateRequest) returns (AuthenticateResponse);
  rpc RefreshToken(RefreshTokenRequest) returns (RefreshTokenResponse);
  rpc Logout(LogoutRequest) returns (LogoutResponse);
}

message AuthenticateRequest {
  string phone_number = 1;
  string pin = 2;                          // Plain text PIN (validated against bcrypt)
  string device_id = 3;                    // Device fingerprint
  bool is_biometric = 4;                   // True if credentials from biometric keystore
  string tenant_id = 5;
}

message AuthenticateResponse {
  bool success = 1;
  string access_token = 2;                 // JWT, 15-minute expiry
  string refresh_token = 3;               // Opaque token, 30-day expiry
  int64 access_token_expires_in = 4;      // Seconds until expiry
  int64 refresh_token_expires_in = 5;
  string account_id = 6;
  string message = 7;
  int32 remaining_attempts = 8;           // If auth failed, how many attempts left
  int64 lockout_remaining_seconds = 9;    // If locked out, seconds remaining
}

message RefreshTokenRequest {
  string refresh_token = 1;
  string device_id = 2;
}

message RefreshTokenResponse {
  bool success = 1;
  string access_token = 2;
  string refresh_token = 3;
  int64 access_token_expires_in = 4;
  int64 refresh_token_expires_in = 5;
  string message = 6;
}

message LogoutRequest {
  string account_id = 1;
  bool all_devices = 2;                   // True to invalidate all sessions
}

message LogoutResponse {
  bool success = 1;
  string message = 2;
}
```

### Database Changes

**Table:** `refresh_tokens` (schema: `{tenant_schema}`)

```sql
CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    token_hash VARCHAR(64) NOT NULL,         -- SHA-256 hash of refresh token
    token_family UUID NOT NULL,              -- Groups tokens for rotation chain
    device_id VARCHAR(64) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    revoked_at TIMESTAMPTZ,
    revoked_reason VARCHAR(100),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_refresh_tokens_account FOREIGN KEY (account_id) REFERENCES accounts(id)
);

CREATE INDEX idx_refresh_tokens_hash ON refresh_tokens(token_hash);
CREATE INDEX idx_refresh_tokens_account ON refresh_tokens(account_id);
CREATE INDEX idx_refresh_tokens_family ON refresh_tokens(token_family);
CREATE INDEX idx_refresh_tokens_expires ON refresh_tokens(expires_at) WHERE NOT is_revoked;
```

### Redis Schema

```
# Failed attempt counter
Key:     auth:failures:{tenant_id}:{account_id}
Value:   Integer (failure count)
TTL:     30 minutes (auto-resets after lockout period)

# Lockout flag
Key:     auth:lockout:{tenant_id}:{account_id}
Value:   "1"
TTL:     30 minutes (1800 seconds)
```

### JWT Claims Structure

```json
{
  "sub": "account-uuid",
  "tenant_id": "tenant-uuid",
  "device_id": "sha256-device-fingerprint",
  "roles": ["user"],
  "jti": "unique-token-id",
  "iat": 1740000000,
  "exp": 1740000900
}
```

### Security Considerations
- **PIN Transport:** PIN is sent over TLS-encrypted gRPC channel. Never logged, even in debug mode.
- **Bcrypt Cost Factor:** Use cost factor 12 (approximately 250ms hash time) balancing security and server load.
- **JWT Signing:** Use RS256 (RSA-SHA256) with a 2048-bit key. Private key stored in HSM or secure vault. Public key available for validation by all services.
- **Refresh Token Rotation:** Critical for detecting token theft. If a refresh token is reused after it has been rotated (indicating the old token was stolen), ALL tokens in the family are revoked.
- **Lockout Bypass:** No administrative bypass for lockout. 30-minute timeout is the only reset mechanism. This prevents social engineering of support staff.
- **Brute Force Protection:** 5 attempts x 4-digit PIN = 1 in 2,000 chance of guessing correctly. Lockout after 5 attempts makes brute force infeasible.
- **Token Storage:** Client stores access token in memory (not persistent storage). Refresh token in Android Keystore (encrypted, hardware-backed where available).
- **Constant-Time Comparison:** Bcrypt comparison is inherently constant-time. Token hash comparison must also use constant-time comparison to prevent timing attacks.

### Edge Cases
- **Simultaneous login from same device:** Allow; each generates a new token pair. Old tokens remain valid until expiry.
- **Login during lockout:** Return `PERMISSION_DENIED` with remaining lockout seconds. Do not reveal whether the account exists.
- **Refresh token used after logout:** Token is revoked during logout. Refresh attempt returns `UNAUTHENTICATED`. User must re-login.
- **Expired refresh token:** Return `UNAUTHENTICATED` with message indicating re-authentication is required.
- **Token family compromise (replay of rotated token):** Revoke ALL tokens in the family. User must re-authenticate on all devices (though currently single-device).
- **Clock skew between servers:** JWT `exp` validation should allow a 30-second grace period to account for minor clock differences.
- **Account suspended during active session:** Token validation should check account status. Suspended accounts should have all tokens revoked.
- **Redis unavailable for lockout check:** Fail open (allow authentication) but log a critical warning. This is a security trade-off for availability. Alternative: use a database fallback for lockout tracking.

---

## Dependencies

**Prerequisite Stories:**
- STORY-010: PIN Setup during Registration (PIN hash must exist)
- STORY-005: Project Infrastructure Setup (JWT signing keys, Redis)

**Blocked Stories:**
- STORY-019: Session Management & Auto-Timeout (requires JWT tokens)
- STORY-020: Transaction Authorization (requires PIN validation capability)

**External Dependencies:**
- Redis for lockout tracking
- HSM or secure vault for JWT signing key storage
- BCrypt library (BCrypt.Net-Next NuGet package)
- JWT library (Microsoft.AspNetCore.Authentication.JwtBearer)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] PIN authentication verified: correct PIN succeeds, wrong PIN fails
- [ ] Lockout verified: 5 failures trigger 30-minute lockout, counter resets on success
- [ ] JWT tokens verified: correct claims, proper expiry, valid signature
- [ ] Refresh token rotation verified: old token invalidated, new token issued
- [ ] Token family revocation verified: replaying a rotated token revokes the entire family
- [ ] Logout verified: all tokens invalidated
- [ ] Device binding check verified: mismatched device rejected
- [ ] Security audit: no PINs logged, no tokens in plain text in DB

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
