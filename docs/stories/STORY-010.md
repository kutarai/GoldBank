# STORY-010: Create Account PIN

**Epic:** EPIC-001 User Registration & KYC
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a new user,
I want to create a 4-6 digit PIN,
So that I can secure my account.

---

## Description

### Background
After successful phone verification (STORY-009), the user must create a PIN to secure their account. The PIN is the primary authentication mechanism for all financial transactions in GoldBank -- payments, transfers, cash-out, and bill payments all require PIN confirmation. Given the target demographic (unbanked consumers in Southern Africa), PINs are preferred over passwords because they are simpler to remember and faster to enter on mobile devices and POS terminals.

PIN security is critical. The PIN must be hashed with bcrypt (cost factor 12) before storage, and common weak patterns (sequential digits like `1234`, repeated digits like `1111`) must be rejected. The user must enter the PIN twice for confirmation to prevent typos.

This story completes the registration flow. After PIN creation, the account transitions from a temporary auth state to a fully authenticated state with a standard JWT.

### Scope

**In scope:**
- `AccountService.CreatePIN` gRPC endpoint implementation
- PIN validation: length (4-6 digits), pattern rejection, confirmation match
- PIN hashing with bcrypt (cost factor 12)
- Storage of `pin_hash` in the `accounts` table
- Full JWT issuance (replacing the temporary token from STORY-009)
- Refresh token generation and storage
- `PINCreated` domain event publishing via Wolverine
- Error handling for all failure scenarios

**Out of scope:**
- PIN change flow (future sprint)
- PIN reset flow (future sprint, requires OTP re-verification)
- Biometric authentication as PIN alternative
- PIN encryption in transit (handled by gRPC TLS)
- PIN attempt tracking and lockout (handled in authentication story)

### User Flow

**PIN Creation Flow:**
1. User completes OTP verification (STORY-009) and receives a temporary token
2. App presents PIN creation screen
3. User enters a 4-6 digit PIN
4. App presents PIN confirmation screen
5. User re-enters the PIN
6. App calls `AccountService.CreatePIN` with the temporary token, account_id, PIN, and confirmation
7. Server validates the temporary token scope (`pin_creation`)
8. Server validates PIN: length, no sequential patterns, no repeated patterns
9. Server confirms PIN matches confirmation
10. Server hashes PIN with bcrypt (cost factor 12)
11. Server stores `pin_hash` in the account record
12. Server generates full JWT access token and refresh token
13. Server publishes `PINCreated` domain event
14. Server returns tokens to the app
15. App stores tokens securely and navigates to the home screen

---

## Acceptance Criteria

- [ ] `AccountService.CreatePIN` accepts `account_id`, `pin`, and `pin_confirmation`
- [ ] Endpoint requires a valid temporary token with `pin_creation` scope
- [ ] PIN length is validated: minimum 4 digits, maximum 6 digits
- [ ] PIN rejects sequential patterns: `1234`, `4321`, `2345`, `5678`, `0123`, `9876`, etc.
- [ ] PIN rejects repeated digit patterns: `1111`, `2222`, `0000`, `3333`, etc.
- [ ] PIN rejects common weak PINs: `1234`, `0000`, `1111`, `9999`, `1357`, `2468`
- [ ] PIN and PIN confirmation must match exactly
- [ ] PIN is hashed using bcrypt with cost factor 12
- [ ] Hashed PIN is stored in `pin_hash` column of the `accounts` table
- [ ] Raw PIN is never logged, stored in plaintext, or returned in any response
- [ ] If PIN is already set for the account, return an error (prevent re-creation)
- [ ] A full JWT access token is returned with claims: `sub`, `tenant_id`, `device_id`, `roles`
- [ ] A refresh token is generated and stored
- [ ] `PINCreated` domain event is published via Wolverine
- [ ] Unit tests cover all validation rules and edge cases (>=80% coverage)

---

## Technical Notes

### Components

**Affected Projects:**
- `GoldBank.Core/Modules/Accounts/` -- PIN creation handler and validation
- `GoldBank.SharedKernel/Events/` -- `PINCreated` event (from STORY-007)

**File Structure:**
```
GoldBank.Core/Modules/Accounts/
  Application/
    Commands/
      CreatePINCommand.cs
    Handlers/
      CreatePINHandler.cs
    Validators/
      PINValidator.cs
  Infrastructure/
    Services/
      PinHashingService.cs
      JwtTokenService.cs
```

### API / gRPC Endpoints

**AccountService.CreatePIN:**
```
Request:
  CreatePINRequest {
    account_id: "uuid-of-account"
    pin: "8472"                    // 4-6 digit PIN
    pin_confirmation: "8472"       // Must match pin
  }

  Headers:
    Authorization: Bearer <temporary_token>  // From VerifyOTP response

Response (success):
  CreatePINResponse {
    success: true
    message: "PIN created successfully. Welcome to GoldBank!"
    auth_token: "eyJhbGciOiJIUzI1NiIs..."    // Full JWT
    refresh_token: "rt_xxxxxxxxxxxxxxxxxxxx"  // Refresh token
  }

Response (invalid PIN format):
  gRPC Status: INVALID_ARGUMENT
  Detail: "PIN must be 4-6 digits"

Response (weak PIN):
  gRPC Status: INVALID_ARGUMENT
  Detail: "PIN is too easy to guess. Avoid sequential or repeated numbers."

Response (mismatch):
  gRPC Status: INVALID_ARGUMENT
  Detail: "PIN and confirmation do not match"

Response (already set):
  gRPC Status: FAILED_PRECONDITION
  Detail: "PIN has already been set for this account"

Response (invalid token scope):
  gRPC Status: PERMISSION_DENIED
  Detail: "Invalid or expired registration token"
```

### Implementation Details

**PINValidator.cs:**
```csharp
public class PINValidator
{
    // Known weak PINs (expanded set)
    private static readonly HashSet<string> WeakPINs = new()
    {
        "0000", "1111", "2222", "3333", "4444",
        "5555", "6666", "7777", "8888", "9999",
        "1234", "4321", "0123", "9876",
        "2345", "3456", "4567", "5678", "6789",
        "9870", "8765", "7654", "6543", "5432",
        "1357", "2468", "1470", "2580", "0852",
        "00000", "11111", "12345", "54321",
        "000000", "111111", "123456", "654321"
    };

    public static Result Validate(string pin, string confirmation)
    {
        // Check null/empty
        if (string.IsNullOrWhiteSpace(pin))
            return Result.Failure("PIN is required");

        // Check digits only
        if (!pin.All(char.IsDigit))
            return Result.Failure("PIN must contain only digits");

        // Check length
        if (pin.Length < 4 || pin.Length > 6)
            return Result.Failure("PIN must be 4-6 digits");

        // Check confirmation match
        if (pin != confirmation)
            return Result.Failure("PIN and confirmation do not match");

        // Check weak PINs
        if (WeakPINs.Contains(pin))
            return Result.Failure(
                "PIN is too easy to guess. Avoid sequential or repeated numbers.");

        // Check for sequential patterns (ascending)
        if (IsSequential(pin))
            return Result.Failure(
                "PIN is too easy to guess. Avoid sequential numbers.");

        // Check for all same digits
        if (pin.Distinct().Count() == 1)
            return Result.Failure(
                "PIN is too easy to guess. Avoid repeated numbers.");

        return Result.Success();
    }

    private static bool IsSequential(string pin)
    {
        // Check ascending sequential: each digit is previous + 1
        var ascending = true;
        var descending = true;
        for (int i = 1; i < pin.Length; i++)
        {
            if (pin[i] - pin[i - 1] != 1) ascending = false;
            if (pin[i - 1] - pin[i] != 1) descending = false;
        }
        return ascending || descending;
    }
}
```

**PinHashingService.cs:**
```csharp
public class PinHashingService
{
    private const int BCRYPT_WORK_FACTOR = 12;

    /// <summary>
    /// Hashes a PIN using bcrypt with the configured work factor.
    /// The raw PIN is not retained after this call.
    /// </summary>
    public string HashPin(string pin)
    {
        return BCrypt.Net.BCrypt.HashPassword(pin, BCRYPT_WORK_FACTOR);
    }

    /// <summary>
    /// Verifies a PIN against a stored bcrypt hash.
    /// Used during authentication (not in this story, but provided for completeness).
    /// </summary>
    public bool VerifyPin(string pin, string hashedPin)
    {
        return BCrypt.Net.BCrypt.Verify(pin, hashedPin);
    }
}
```

**CreatePINHandler.cs:**
```csharp
public class CreatePINHandler
{
    private readonly TenantDbContext _dbContext;
    private readonly PinHashingService _pinHasher;
    private readonly JwtTokenService _tokenService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<CreatePINHandler> _logger;

    public async Task<CreatePINResponse> Handle(CreatePINCommand command)
    {
        // Validate PIN
        var validationResult = PINValidator.Validate(command.Pin, command.PinConfirmation);
        if (!validationResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                validationResult.Error));

        // Load account
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId);

        if (account == null)
            throw new RpcException(new Status(StatusCode.NotFound,
                "Account not found"));

        // Check if PIN already set
        if (!string.IsNullOrEmpty(account.PinHash))
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                "PIN has already been set for this account"));

        // Hash PIN
        account.PinHash = _pinHasher.HashPin(command.Pin);
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Publish domain event
        await _messageBus.PublishAsync(new PINCreated
        {
            AccountId = account.Id,
            TenantId = command.TenantId
        });

        // Generate full auth tokens
        var (accessToken, refreshToken) = await _tokenService.GenerateTokenPairAsync(account);

        _logger.LogInformation(
            "PIN created for account {AccountId}",
            account.Id);

        return new CreatePINResponse
        {
            Success = true,
            Message = "PIN created successfully. Welcome to GoldBank!",
            AuthToken = accessToken,
            RefreshToken = refreshToken
        };
    }
}
```

**JwtTokenService.cs:**
```csharp
public class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly IConnectionMultiplexer _redis;

    public async Task<(string accessToken, string refreshToken)> GenerateTokenPairAsync(
        Account account)
    {
        // Access Token
        var claims = new[]
        {
            new Claim("sub", account.Id.ToString()),
            new Claim("tenant_id", account.TenantId.ToString()),
            new Claim("phone", account.Phone),
            new Claim("role", "customer"), // Default role
            new Claim("kyc_level", account.KYCLevel.ToString()),
            new Claim("jti", Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var accessToken = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
            signingCredentials: creds);

        var accessTokenString = new JwtSecurityTokenHandler().WriteToken(accessToken);

        // Refresh Token
        var refreshToken = $"rt_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}";

        // Store refresh token in Redis with expiry
        var db = _redis.GetDatabase();
        var refreshKey = $"refresh_token:{account.Id}:{refreshToken}";
        await db.StringSetAsync(refreshKey, account.Id.ToString(),
            TimeSpan.FromDays(_settings.RefreshTokenExpiryDays));

        return (accessTokenString, refreshToken);
    }
}
```

### Database Changes

**Modified Column in `accounts` table:**
- `pin_hash` column is populated with bcrypt hash (VARCHAR(255))

No schema changes required -- the column already exists from STORY-003.

**Redis Keys Used:**
- `refresh_token:{account_id}:{token}` -- String: account_id, TTL: 30 days

### Security Considerations
- **PIN never in plaintext after hashing:** The raw PIN exists only in memory during the hash operation and is not stored, logged, or returned
- **bcrypt cost factor 12:** Provides approximately 250ms hash time, making brute-force attacks impractical (~4 hashes/second per core)
- **Timing-safe comparison:** bcrypt.Verify handles timing-safe comparison internally
- **Token scope validation:** The `pin_creation` scope in the temporary token prevents reuse for other operations
- **One-time PIN creation:** PIN cannot be re-created without going through a separate PIN reset flow (future story)
- **PIN validation on server side only:** Never trust client-side validation; always re-validate on server
- **Refresh token security:** Refresh tokens are stored in Redis and associated with a specific account; they cannot be used for a different account
- **PIN entry should be masked on the client:** The app should display dots/asterisks (client-side concern, documented for mobile team)

### Edge Cases
- User creates PIN but network fails before receiving response: Account has PIN set, but user sees error. On retry, "PIN already set" error tells the user to log in instead.
- Temporary token expires before PIN creation: User must re-register (new OTP). The account record created during VerifyOTP is orphaned (pending_kyc with no PIN). A cleanup job should remove these after 24 hours.
- bcrypt library unavailable or fails: Return `INTERNAL` error; do not fall back to weaker hashing
- PIN with leading zeros: Must be handled correctly as a string, not integer (e.g., `0073` is valid)
- Concurrent PIN creation attempts: Database row-level locking prevents both from succeeding; first one wins
- Empty PIN confirmation: Validation catches this as a mismatch
- Unicode digits or special characters in PIN: Validation ensures ASCII digits only (`0-9`)
- bcrypt hash exceeds column length: bcrypt output is always 60 characters; VARCHAR(255) is sufficient

---

## Dependencies

**Prerequisite Stories:**
- STORY-009: User Self-Registration (account must exist, temporary token must be issued)
- STORY-003: PostgreSQL Database Schema (accounts table with `pin_hash` column)
- STORY-007: Wolverine Messaging (for `PINCreated` event publishing)

**Blocked Stories:**
- All transaction stories that require PIN authentication (Sprint 2+)
- PIN change/reset flows (future sprints)

**External Dependencies:**
- `BCrypt.Net-Next` NuGet package for PIN hashing
- Redis (for refresh token storage)
- JWT signing key (shared with Gateway, configured in appsettings)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) for PIN validator, hashing service, handler
- [ ] Integration tests passing (full CreatePIN flow with database and Redis)
- [ ] Code reviewed and approved
- [ ] Documentation updated (PIN requirements documented, security notes)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
