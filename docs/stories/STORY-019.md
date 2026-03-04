# STORY-019: Session Management & Auto-Timeout

**Epic:** EPIC-014
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a user
I want my session to expire after inactivity
So that my account is protected

---

## Description

### Background
Session management and auto-timeout are essential security features for a mobile banking platform. If a user leaves their phone unattended with the banking app open, or if the phone is stolen while unlocked, an auto-timeout mechanism ensures that the session expires after a period of inactivity, requiring re-authentication before any further actions can be taken.

This is especially critical in UniBank's target market where shared device usage is common (family members sharing a phone) and where devices may be more vulnerable to theft. The default inactivity timeout is 5 minutes, but this is configurable per tenant to allow different operators to balance security and convenience based on their market conditions and regulatory requirements.

The session is tracked in Redis using a sliding window TTL keyed by the JWT's unique token identifier (JTI). Every authenticated API request resets the TTL, effectively extending the session as long as the user remains active. When the TTL expires, the next request will fail with a session-expired error, requiring the user to re-authenticate.

An important design consideration is that active transactions (e.g., a transfer in progress) must not be interrupted by session timeout. The system checks for in-flight transactions before invalidating a session.

### Scope
**In scope:**
- Redis-based session tracking with sliding TTL
- Session key pattern: `session:{token_jti}`
- Default timeout: 5 minutes of inactivity (configurable per tenant)
- Sliding window: every authenticated request resets the TTL
- API Gateway interceptor checks session validity on every request
- Active transaction protection: do not invalidate sessions with in-flight transactions
- Logout endpoint: `AccountService.Logout` invalidates all sessions and tokens
- Session creation on successful authentication (linked to STORY-018)
- Re-authentication required after session expiry (PIN or biometric)

**Out of scope:**
- Session transfer between devices (single device per account)
- Concurrent session management (only one active session per account for initial release)
- Session activity dashboard for user visibility
- Admin-initiated session termination (admin API, future story)
- Configurable timeout by user (tenant-level only for now)

### User Flow

**Normal Session Lifecycle:**
1. User authenticates successfully (STORY-018)
2. Server creates a session entry in Redis: `session:{jti}` with TTL = 300 seconds (5 min)
3. User interacts with the app (views balance, history, etc.)
4. Each API request passes through the Gateway interceptor
5. Interceptor checks if `session:{jti}` exists in Redis
6. If exists: reset TTL to 300 seconds (sliding window), allow request
7. If not exists: return `UNAUTHENTICATED` with reason `SESSION_EXPIRED`
8. App detects session expiry and presents re-authentication screen (PIN or biometric)
9. User re-authenticates; new session created

**Session Timeout During Inactivity:**
1. User stops using the app (puts phone down, switches to another app)
2. No API requests are made for 5 minutes
3. Redis TTL expires; session key is automatically deleted
4. User returns to the app
5. App makes an API request (e.g., refresh home screen)
6. Gateway interceptor finds no session for the JTI
7. Returns `UNAUTHENTICATED` with `SESSION_EXPIRED`
8. App navigates to re-authentication screen

**Active Transaction Protection:**
1. User initiates a transfer (STORY-020+)
2. Transfer is in progress (API call pending)
3. Session would normally timeout during the transfer processing
4. Gateway interceptor checks for active transactions: finds an in-flight transfer
5. Session is extended by the transaction processing time (up to 2 minutes)
6. Transfer completes
7. Normal session timeout resumes

**Logout:**
1. User taps "Logout" in the app menu
2. App calls `AccountService.Logout`
3. Server deletes the session key from Redis
4. Server revokes the refresh token in the database
5. Server publishes `SessionTerminated` event
6. Returns success
7. App navigates to the login screen

---

## Acceptance Criteria

- [ ] Session is created in Redis on successful authentication with a configurable TTL (default 5 minutes)
- [ ] Every authenticated API request resets (extends) the session TTL (sliding window)
- [ ] Session timeout is configurable per tenant
- [ ] When the session expires (TTL reaches zero), the next API request returns `UNAUTHENTICATED` with reason `SESSION_EXPIRED`
- [ ] User must re-authenticate (PIN or biometric) after session expiry
- [ ] Active transactions are not interrupted by session timeout; the session is extended during in-flight operations
- [ ] `Logout` endpoint invalidates the session in Redis and revokes the refresh token
- [ ] Only one active session per account is allowed; new login invalidates the previous session
- [ ] Session state includes the last activity timestamp for debugging and audit purposes
- [ ] Tokens are invalidated (access and refresh) on explicit logout

---

## Technical Notes

### Components
- **AuthModule** (`src/Modules/Auth/`):
  - `SessionService.cs`: Session creation, validation, extension, and termination
  - `SessionConfiguration.cs`: Tenant-specific timeout configuration
- **ApiGateway** (`src/ApiGateway/`):
  - `SessionValidationInterceptor.cs`: gRPC interceptor checking session validity on every request
  - Chains with `DeviceValidationInterceptor` (STORY-014)
- **AccountModule** (`src/Modules/Account/`):
  - `AccountService.cs`: `Logout` endpoint implementation
- **Infrastructure** (`src/Infrastructure/`):
  - `RedisSessionStore.cs`: Redis operations for session management

### API / gRPC Endpoints

**Service:** `AccountService`

```protobuf
service AccountService {
  rpc Logout(LogoutRequest) returns (LogoutResponse);      // From STORY-018, enhanced
}

// LogoutRequest and LogoutResponse defined in STORY-018
```

No new gRPC endpoints beyond what is defined in STORY-018. Session management is handled transparently by the Gateway interceptor.

**Gateway Interceptor:**

```csharp
public class SessionValidationInterceptor : Interceptor
{
    private readonly ISessionService _sessionService;
    private readonly IActiveTransactionTracker _transactionTracker;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        // Skip session check for unauthenticated endpoints (login, register, etc.)
        if (IsPublicEndpoint(context.Method))
            return await continuation(request, context);

        var jti = context.GetHttpContext().User.FindFirst("jti")?.Value;
        var accountId = context.GetHttpContext().User.FindFirst("sub")?.Value;

        // Check session exists
        var session = await _sessionService.GetSessionAsync(jti);
        if (session == null)
        {
            // Check for active transactions before rejecting
            if (await _transactionTracker.HasActiveTransaction(accountId))
            {
                // Extend session for transaction completion
                await _sessionService.CreateSessionAsync(jti, accountId, TimeSpan.FromMinutes(2));
            }
            else
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "SESSION_EXPIRED"));
            }
        }

        // Refresh sliding window
        await _sessionService.ExtendSessionAsync(jti);

        return await continuation(request, context);
    }
}
```

### Database Changes

No new database tables. Session state is entirely in Redis. Refresh token revocation uses the `refresh_tokens` table from STORY-018.

### Redis Schema

```
# Session tracking
Key:     session:{jti}
Value:   JSON SessionData
TTL:     Configurable per tenant (default 300 seconds / 5 minutes)

SessionData:
{
  "account_id": "uuid",
  "tenant_id": "uuid",
  "device_id": "sha256-fingerprint",
  "created_at": "2026-02-24T10:30:00Z",
  "last_activity": "2026-02-24T10:34:00Z"
}

# Active transaction tracking (used by transaction services)
Key:     active_tx:{account_id}
Value:   JSON { "transaction_id": "uuid", "type": "transfer", "started_at": "..." }
TTL:     120 seconds (2-minute max transaction duration)

# Tenant session configuration
Key:     config:session_timeout:{tenant_id}
Value:   Integer (timeout in seconds)
TTL:     None (persistent, refreshed on config change)
```

### Security Considerations
- **Session Fixation:** Sessions are created only by the server on successful authentication. The JTI is generated server-side and cannot be influenced by the client.
- **Session Hijacking:** The session is tied to the JTI in the JWT, which is signed. Stealing the session key alone is not sufficient; the attacker would also need the JWT.
- **Single Session Enforcement:** When a new session is created (login), any previous session for the same account is invalidated. This prevents concurrent access from multiple stolen tokens.
- **Logout Completeness:** Logout invalidates both the session (Redis) and the refresh token (database). The access token remains valid until expiry (max 15 minutes) but the session check at the gateway prevents its use.
- **Redis Failure:** If Redis is unavailable, the gateway cannot validate sessions. For security, this should fail closed (deny access) rather than fail open. Log critical alerts.
- **Transaction Protection Abuse:** The active transaction extension is limited to 2 minutes maximum. This prevents an attacker from keeping a session alive indefinitely by continuously creating fake transactions.

### Edge Cases
- **Multiple rapid requests:** Each request resets the TTL independently. Redis handles concurrent SET operations atomically. No race condition.
- **Request arrives exactly at TTL expiry:** Redis TTL is checked atomically. If the key has expired, the request is rejected. The client should not rely on millisecond-level timing.
- **Logout from unresponsive session:** If the session has already expired when the user taps logout, the logout should still succeed (revoke refresh token, return success).
- **App backgrounded on Android:** The app may be killed by the OS. On resume, the session may have expired. The app should always check session validity on resume and re-authenticate if needed.
- **Network latency during session refresh:** If a request takes longer than the session TTL to reach the server, the session may expire before the request is processed. The interceptor should extend the session upon receiving the request (before processing).
- **Config change for session timeout:** Updated tenant configuration is picked up on the next session creation. Existing sessions retain their original TTL until they expire or are refreshed.
- **Account suspended while session active:** Token validation (separate from session validation) should check account status. Suspended accounts should have sessions forcibly terminated.

---

## Dependencies

**Prerequisite Stories:**
- STORY-018: PIN & Biometric Authentication (session created on successful auth, JTI from JWT)

**Blocked Stories:**
- None directly, but session management is a foundational security feature for all authenticated operations

**External Dependencies:**
- Redis instance (shared with STORY-016 balance cache and STORY-018 lockout)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Session creation verified on successful authentication
- [ ] Sliding window verified: active requests extend the session TTL
- [ ] Session expiry verified: inactive session returns SESSION_EXPIRED after timeout
- [ ] Active transaction protection verified: session not expired during in-flight transaction
- [ ] Logout verified: session deleted, refresh token revoked
- [ ] Single session enforcement verified: new login invalidates previous session
- [ ] Tenant-configurable timeout verified with different timeout values
- [ ] Redis failure behavior verified: requests denied when Redis is unavailable

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
