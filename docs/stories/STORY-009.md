# STORY-009: User Self-Registration with Phone & OTP

**Epic:** EPIC-001 User Registration & KYC
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As an unbanked consumer,
I want to register using my phone number with OTP verification,
So that I can create a GoldBank account.

---

## Description

### Background
GoldBank targets the unbanked population in Southern Africa where mobile phone penetration far exceeds bank account ownership. The registration flow is deliberately simple: a user provides their phone number, receives an OTP via SMS, verifies it, and an account is created in `pending_kyc` status. This low-friction onboarding is critical for adoption -- every additional step risks losing potential users.

The registration flow is the first feature-level story and represents the first end-to-end gRPC request flowing through the Gateway, interceptors, Core Banking service, and database. It also triggers the first domain event (`UserRegistered`, `AccountCreated`) through the Wolverine messaging system.

Phone numbers must support the E.164 international format with primary support for Southern African country codes (+27 South Africa, +263 Zimbabwe, +260 Zambia, +258 Mozambique, +267 Botswana, +266 Lesotho, +268 Eswatini).

### Scope

**In scope:**
- `AccountService.Register` gRPC endpoint implementation
- `AccountService.VerifyOTP` gRPC endpoint implementation
- Phone number validation (E.164 format, Southern African codes)
- Duplicate phone number detection
- OTP generation (6-digit, cryptographically random)
- OTP storage in Redis with 5-minute TTL (hashed)
- SMS gateway integration interface (with mock implementation for dev)
- Account record creation in tenant database
- Domain event publishing: `UserRegistered`, `AccountCreated`
- Rate limiting: 3 OTP requests per phone number per hour
- Temporary auth token generation after OTP verification

**Out of scope:**
- PIN creation (STORY-010)
- KYC document upload (future sprint)
- Social login or alternative authentication methods
- Biometric registration
- Account recovery flow
- Phone number change flow

### User Flow

**Registration Flow:**
1. User opens GoldBank mobile app for the first time
2. User enters their phone number (e.g., `+27821234567`)
3. App calls `AccountService.Register` via the Gateway
4. Server validates phone format and checks for duplicates
5. Server generates 6-digit OTP, stores hash in Redis, sends via SMS
6. Server returns `RegisterResponse` with `registration_id` and OTP metadata
7. User receives SMS with OTP within 30 seconds
8. User enters OTP in the app
9. App calls `AccountService.VerifyOTP` with `registration_id` and OTP
10. Server validates OTP against Redis hash
11. Server creates account record (status: `pending_kyc`) in tenant database
12. Server publishes `UserRegistered` and `AccountCreated` domain events
13. Server returns `VerifyOTPResponse` with `account_id` and `temporary_token`
14. App navigates to PIN creation screen (STORY-010)

**Error Flows:**
- Invalid phone format: Return error immediately
- Duplicate phone: Return error "Phone number already registered"
- OTP expired: Return error "OTP has expired, please request a new one"
- Wrong OTP: Return error "Invalid OTP" (track attempts, lock after 5 failures)
- Rate limited: Return error "Too many requests, try again in X minutes"

---

## Acceptance Criteria

- [ ] `AccountService.Register` accepts a phone number in E.164 format and returns a `registration_id`
- [ ] Phone number is validated for E.164 format and supported country codes (+27, +263, +260, +258, +267, +266, +268)
- [ ] Duplicate phone numbers are detected and return an appropriate error
- [ ] A 6-digit OTP is generated using a cryptographically secure random number generator
- [ ] OTP is hashed (SHA-256) before storage in Redis with a 5-minute TTL
- [ ] SMS is sent to the provided phone number via the SMS gateway interface
- [ ] `AccountService.VerifyOTP` validates the OTP and creates an account
- [ ] Account is created with status `pending_kyc` in the correct tenant schema
- [ ] Account record includes: `id` (UUID), `phone`, `phone_country_code`, `status`, `tenant_id`, `created_at`
- [ ] `UserRegistered` domain event is published via Wolverine after successful registration
- [ ] `AccountCreated` domain event is published via Wolverine after account creation
- [ ] A temporary JWT token is returned after OTP verification (valid for 30 minutes, limited scope: PIN creation only)
- [ ] Rate limiting enforces maximum 3 OTP requests per phone number per hour
- [ ] Rate limiting returns `RESOURCE_EXHAUSTED` gRPC status with retry-after information
- [ ] Wrong OTP attempts are tracked; after 5 consecutive failures, the OTP is invalidated
- [ ] Unit tests cover all validation, OTP generation, and account creation logic
- [ ] Integration test demonstrates full Register -> VerifyOTP flow

---

## Technical Notes

### Components

**Affected Projects:**
- `GoldBank.Core/Modules/Accounts/` -- Domain entities, application services, gRPC service implementation
- `GoldBank.SharedKernel/Events/` -- `UserRegistered`, `AccountCreated` events (from STORY-007)
- `GoldBank.Gateway` -- Routes `AccountService` calls (from STORY-005)

**File Structure:**
```
GoldBank.Core/Modules/Accounts/
  Domain/
    Entities/
      Account.cs
    ValueObjects/
      PhoneNumber.cs
    Events/
      (uses SharedKernel events)
  Application/
    Commands/
      RegisterCommand.cs
      VerifyOTPCommand.cs
    Handlers/
      RegisterHandler.cs
      VerifyOTPHandler.cs
    Validators/
      RegisterCommandValidator.cs
      VerifyOTPCommandValidator.cs
    Interfaces/
      ISmsGateway.cs
      IOtpService.cs
  Infrastructure/
    Persistence/
      AccountEntityConfiguration.cs
    Services/
      OtpService.cs
      MockSmsGateway.cs
  Grpc/
    AccountGrpcService.cs
```

### API / gRPC Endpoints

**AccountService.Register:**
```
Request:
  RegisterRequest {
    phone_number: "+27821234567"   // E.164 format
    device_id: "abc123-device-id"  // Mobile device identifier
    tenant_id: "uuid-of-tenant"    // Tenant identifier
  }

Response (success):
  RegisterResponse {
    success: true
    message: "OTP sent successfully"
    registration_id: "uuid-registration-ref"
    otp_length: 6
    otp_ttl_seconds: 300
  }

Response (duplicate):
  gRPC Status: ALREADY_EXISTS
  Detail: "Phone number is already registered"

Response (rate limited):
  gRPC Status: RESOURCE_EXHAUSTED
  Detail: "Rate limit exceeded. Try again in 45 minutes."

Response (invalid phone):
  gRPC Status: INVALID_ARGUMENT
  Detail: "Invalid phone number format. Expected E.164 format."
```

**AccountService.VerifyOTP:**
```
Request:
  VerifyOTPRequest {
    registration_id: "uuid-registration-ref"
    otp: "483921"
    phone_number: "+27821234567"
  }

Response (success):
  VerifyOTPResponse {
    success: true
    message: "Phone verified. Please create your PIN."
    account_id: "uuid-of-new-account"
    temporary_token: "eyJhbGciOiJIUzI1NiIs..."  // JWT
  }

Response (invalid OTP):
  gRPC Status: UNAUTHENTICATED
  Detail: "Invalid OTP. 4 attempts remaining."

Response (expired OTP):
  gRPC Status: UNAUTHENTICATED
  Detail: "OTP has expired. Please request a new one."

Response (locked):
  gRPC Status: PERMISSION_DENIED
  Detail: "OTP verification locked due to too many failed attempts."
```

### Implementation Details

**PhoneNumber Value Object:**
```csharp
public class PhoneNumber : ValueObject
{
    private static readonly Regex E164Regex = new(
        @"^\+(?:27|263|260|258|267|266|268)\d{8,9}$",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> CountryCodes = new()
    {
        ["+27"] = "ZAF",  // South Africa
        ["+263"] = "ZWE", // Zimbabwe
        ["+260"] = "ZMB", // Zambia
        ["+258"] = "MOZ", // Mozambique
        ["+267"] = "BWA", // Botswana
        ["+266"] = "LSO", // Lesotho
        ["+268"] = "SWZ", // Eswatini
    };

    public string Value { get; }
    public string CountryCode { get; }

    private PhoneNumber(string value, string countryCode)
    {
        Value = value;
        CountryCode = countryCode;
    }

    public static Result<PhoneNumber> Create(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Result<PhoneNumber>.Failure("Phone number is required");

        var normalized = phoneNumber.Trim().Replace(" ", "");
        if (!E164Regex.IsMatch(normalized))
            return Result<PhoneNumber>.Failure(
                "Invalid phone number format. Expected E.164 with Southern African country code.");

        var countryCode = CountryCodes.Keys.First(cc => normalized.StartsWith(cc));
        return Result<PhoneNumber>.Success(
            new PhoneNumber(normalized, CountryCodes[countryCode]));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**OTP Service:**
```csharp
public class OtpService : IOtpService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OtpService> _logger;
    private const int OTP_LENGTH = 6;
    private const int OTP_TTL_SECONDS = 300; // 5 minutes
    private const int MAX_ATTEMPTS = 5;
    private const int RATE_LIMIT_PER_HOUR = 3;

    public async Task<Result<string>> GenerateAndStoreOTPAsync(
        string phoneNumber, string registrationId)
    {
        var db = _redis.GetDatabase();

        // Check rate limit
        var rateLimitKey = $"otp:ratelimit:{phoneNumber}";
        var requestCount = await db.StringIncrementAsync(rateLimitKey);
        if (requestCount == 1)
            await db.KeyExpireAsync(rateLimitKey, TimeSpan.FromHours(1));
        if (requestCount > RATE_LIMIT_PER_HOUR)
        {
            var ttl = await db.KeyTimeToLiveAsync(rateLimitKey);
            return Result<string>.Failure(
                $"Rate limit exceeded. Try again in {ttl?.TotalMinutes:F0} minutes.");
        }

        // Generate cryptographically secure OTP
        var otp = GenerateSecureOTP();

        // Hash OTP before storing
        var hashedOtp = HashOTP(otp);

        // Store in Redis with TTL
        var otpKey = $"otp:{registrationId}";
        var otpData = new HashEntry[]
        {
            new("hash", hashedOtp),
            new("phone", phoneNumber),
            new("attempts", 0)
        };

        await db.HashSetAsync(otpKey, otpData);
        await db.KeyExpireAsync(otpKey, TimeSpan.FromSeconds(OTP_TTL_SECONDS));

        return Result<string>.Success(otp);
    }

    public async Task<Result<bool>> ValidateOTPAsync(
        string registrationId, string otp, string phoneNumber)
    {
        var db = _redis.GetDatabase();
        var otpKey = $"otp:{registrationId}";

        // Check if OTP exists
        var exists = await db.KeyExistsAsync(otpKey);
        if (!exists)
            return Result<bool>.Failure("OTP has expired. Please request a new one.");

        // Verify phone number matches
        var storedPhone = await db.HashGetAsync(otpKey, "phone");
        if (storedPhone != phoneNumber)
            return Result<bool>.Failure("Phone number mismatch.");

        // Check attempt count
        var attempts = (int)await db.HashGetAsync(otpKey, "attempts");
        if (attempts >= MAX_ATTEMPTS)
        {
            await db.KeyDeleteAsync(otpKey);
            return Result<bool>.Failure(
                "OTP verification locked due to too many failed attempts.");
        }

        // Validate OTP hash
        var storedHash = (string?)await db.HashGetAsync(otpKey, "hash");
        var providedHash = HashOTP(otp);

        if (storedHash != providedHash)
        {
            await db.HashIncrementAsync(otpKey, "attempts");
            var remaining = MAX_ATTEMPTS - attempts - 1;
            return Result<bool>.Failure(
                $"Invalid OTP. {remaining} attempts remaining.");
        }

        // OTP is valid -- delete it
        await db.KeyDeleteAsync(otpKey);
        return Result<bool>.Success(true);
    }

    private static string GenerateSecureOTP()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var number = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1_000_000;
        return number.ToString().PadLeft(OTP_LENGTH, '0');
    }

    private static string HashOTP(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToBase64String(bytes);
    }
}
```

**Register Handler:**
```csharp
public class RegisterHandler
{
    private readonly TenantDbContext _dbContext;
    private readonly IOtpService _otpService;
    private readonly ISmsGateway _smsGateway;
    private readonly ILogger<RegisterHandler> _logger;

    public async Task<RegisterResponse> Handle(RegisterCommand command)
    {
        // Validate phone number
        var phoneResult = PhoneNumber.Create(command.PhoneNumber);
        if (!phoneResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                phoneResult.Error));

        // Check for duplicate
        var existingAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Phone == phoneResult.Value.Value);
        if (existingAccount != null)
            throw new RpcException(new Status(StatusCode.AlreadyExists,
                "Phone number is already registered"));

        // Generate registration ID
        var registrationId = Guid.NewGuid().ToString();

        // Generate and store OTP
        var otpResult = await _otpService.GenerateAndStoreOTPAsync(
            phoneResult.Value.Value, registrationId);
        if (!otpResult.IsSuccess)
            throw new RpcException(new Status(StatusCode.ResourceExhausted,
                otpResult.Error));

        // Send SMS
        await _smsGateway.SendOtpAsync(
            phoneResult.Value.Value,
            $"Your GoldBank verification code is: {otpResult.Value}. Valid for 5 minutes.");

        _logger.LogInformation(
            "OTP sent for registration {RegistrationId} to phone {Phone}",
            registrationId,
            phoneResult.Value.Value[..^4] + "****"); // Partial masking in logs

        return new RegisterResponse
        {
            Success = true,
            Message = "OTP sent successfully",
            RegistrationId = registrationId,
            OtpLength = 6,
            OtpTtlSeconds = 300
        };
    }
}
```

**Verify OTP Handler:**
```csharp
public class VerifyOTPHandler
{
    private readonly TenantDbContext _dbContext;
    private readonly IOtpService _otpService;
    private readonly IMessageBus _messageBus; // Wolverine
    private readonly JwtSettings _jwtSettings;

    public async Task<VerifyOTPResponse> Handle(VerifyOTPCommand command)
    {
        // Validate OTP
        var validationResult = await _otpService.ValidateOTPAsync(
            command.RegistrationId, command.Otp, command.PhoneNumber);

        if (!validationResult.IsSuccess)
        {
            var statusCode = validationResult.Error.Contains("expired")
                ? StatusCode.Unauthenticated
                : validationResult.Error.Contains("locked")
                    ? StatusCode.PermissionDenied
                    : StatusCode.Unauthenticated;

            throw new RpcException(new Status(statusCode, validationResult.Error));
        }

        // Create account
        var phoneResult = PhoneNumber.Create(command.PhoneNumber);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Phone = phoneResult.Value.Value,
            PhoneCountryCode = phoneResult.Value.CountryCode,
            Status = "pending_kyc",
            KYCLevel = 0,
            DailyLimit = 1000.00m,
            MonthlyLimit = 5000.00m,
            Balance = 0.00m,
            AvailableBalance = 0.00m,
            Currency = "ZAR", // Default, can be overridden by tenant config
            TenantId = command.TenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        // Publish domain events
        await _messageBus.PublishAsync(new UserRegistered
        {
            AccountId = account.Id,
            PhoneNumber = account.Phone,
            DeviceId = command.DeviceId,
            TenantId = command.TenantId
        });

        await _messageBus.PublishAsync(new AccountCreated
        {
            AccountId = account.Id,
            PhoneNumber = account.Phone,
            Status = account.Status,
            TenantId = command.TenantId
        });

        // Generate temporary token (limited scope: PIN creation only)
        var temporaryToken = GenerateTemporaryToken(account);

        return new VerifyOTPResponse
        {
            Success = true,
            Message = "Phone verified. Please create your PIN.",
            AccountId = account.Id.ToString(),
            TemporaryToken = temporaryToken
        };
    }

    private string GenerateTemporaryToken(Account account)
    {
        var claims = new[]
        {
            new Claim("sub", account.Id.ToString()),
            new Claim("tenant_id", account.TenantId.ToString()),
            new Claim("scope", "pin_creation"), // Limited scope
            new Claim("phone", account.Phone)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

**SMS Gateway Interface:**
```csharp
public interface ISmsGateway
{
    Task<bool> SendOtpAsync(string phoneNumber, string message);
    Task<bool> SendAlertAsync(string phoneNumber, string message);
}

// Mock implementation for development
public class MockSmsGateway : ISmsGateway
{
    private readonly ILogger<MockSmsGateway> _logger;

    public Task<bool> SendOtpAsync(string phoneNumber, string message)
    {
        _logger.LogInformation(
            "[MOCK SMS] To: {Phone}, Message: {Message}",
            phoneNumber, message);
        return Task.FromResult(true);
    }

    public Task<bool> SendAlertAsync(string phoneNumber, string message)
    {
        _logger.LogInformation(
            "[MOCK SMS] Alert To: {Phone}, Message: {Message}",
            phoneNumber, message);
        return Task.FromResult(true);
    }
}
```

### Database Changes

Uses `accounts` table from STORY-003 tenant schema. New account records are created with these initial values:

| Column | Value |
|--------|-------|
| `id` | UUID (generated) |
| `phone` | Validated E.164 number |
| `phone_country_code` | Extracted from phone (e.g., "+27") |
| `status` | `pending_kyc` |
| `kyc_level` | `0` |
| `daily_limit` | `1000.00` |
| `monthly_limit` | `5000.00` |
| `balance` | `0.00` |
| `available_balance` | `0.00` |
| `currency` | `ZAR` (tenant default) |
| `tenant_id` | From request context |

**Redis Keys Used:**
- `otp:{registration_id}` -- Hash: `{hash, phone, attempts}`, TTL: 5 minutes
- `otp:ratelimit:{phone_number}` -- Integer counter, TTL: 1 hour

### Security Considerations
- OTP is hashed (SHA-256) before Redis storage -- never stored in plaintext
- OTP is generated using `RandomNumberGenerator` (CSPRNG), not `Random`
- Rate limiting prevents brute-force OTP guessing (3 requests/hour, 5 attempts per OTP)
- Phone numbers are partially masked in logs (`+27****4567`)
- Temporary JWT has limited scope (`pin_creation`) and short expiry (30 minutes)
- The `Register` and `VerifyOTP` endpoints are marked as anonymous in the auth interceptor (no JWT required)
- The `x-tenant-id` header is required for anonymous endpoints to resolve tenant context
- SMS content should not include the app name in production (to prevent social engineering: "Your code is 123456")

### Edge Cases
- Race condition on duplicate check: Use database unique constraint on `phone` column as final guard
- OTP sent but SMS delivery fails: User can retry (within rate limit); log SMS gateway failures for monitoring
- Redis failure during OTP store: Return `INTERNAL` error; do not silently skip OTP
- User registers, does not verify, registers again: New registration overwrites the previous OTP in Redis
- Phone number with different formatting: Normalize before comparison (strip spaces, ensure `+` prefix)
- Simultaneous OTP requests for same phone: Redis atomic operations prevent race conditions
- Large-scale registration abuse: Per-IP rate limiting at the Gateway level (in addition to per-phone)
- Tenant not found: Return clear error before generating OTP (fail fast)
- Account exists but was soft-deleted: Check `deleted_at IS NULL` in duplicate detection; allow re-registration of deleted accounts

---

## Dependencies

**Prerequisite Stories:**
- STORY-005: API Gateway with gRPC Interceptors (Gateway must be routing calls)
- STORY-003: PostgreSQL Database Schema (accounts table must exist)
- STORY-004: gRPC Proto Definitions (AccountService proto must be compiled)
- STORY-007: Wolverine Messaging (for domain event publishing)

**Blocked Stories:**
- STORY-010: Create Account PIN (requires account to exist)

**External Dependencies:**
- Redis (for OTP storage and rate limiting)
- SMS Gateway service (mock for development; production integration in a separate story)
- JWT signing key (configured in appsettings / environment variables)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) for phone validation, OTP service, handlers
- [ ] Integration tests passing (full Register -> VerifyOTP flow against PostgreSQL and Redis)
- [ ] Code reviewed and approved
- [ ] Documentation updated (registration flow sequence diagram, API usage examples)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
