# STORY-021: HSM Interface Service - Core Operations

**Epic:** EPIC-003 NFC Contactless Payments
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 3

---

## User Story

As a **system**
I want **HSM cryptographic operations available via gRPC**
So that **PIN encryption, key management, and tokenization are secure and compliant with banking standards**

---

## Description

### Background

The Hardware Security Module (HSM) Interface Service is the cryptographic foundation for all secure payment operations in UniBank. Every NFC tap, every QR payment, every PIN verification ultimately depends on keys managed by this service. Southern African banking regulations and PCI-DSS compliance require that cryptographic key material never exist in plaintext outside of a certified HSM boundary. This service wraps the physical HSM via PKCS#11, exposing a clean gRPC API that the rest of the platform consumes.

This is a **satellite service** in UniBank's Modular Monolith architecture — it runs as a separate process with its own lifecycle, connected to the core via gRPC with mandatory mTLS. The satellite design isolates HSM driver dependencies (native PKCS#11 libraries) from the main application and allows independent scaling and hardware affinity (the service must run on hosts with physical or network HSM access).

**Functional Requirements:** FR-033 (HSM Integration), FR-034 (Key Management)

### Scope

**In scope:**
- PKCS#11 interop layer for HSM communication
- gRPC service definition and implementation for all cryptographic operations
- Key hierarchy management (Master Key, Zone Master Keys, Session Keys)
- PIN block encryption and decryption (ISO 9564 Format 0)
- MAC generation and verification (ISO 9797-1 Algorithm 3)
- Token generation for payment tokenization
- Comprehensive audit logging of all HSM operations
- mTLS enforcement on all inbound gRPC connections
- Circuit breaker pattern for HSM connectivity resilience
- Health check endpoint for HSM availability monitoring

**Out of scope:**
- HSM hardware procurement and physical installation
- HSM firmware configuration and initialization ceremony
- Key ceremony procedures (documented separately in operational runbook)
- Certificate Authority (CA) setup for mTLS certificates
- HSM clustering and failover at the hardware level (handled by HSM vendor tools)
- Payment processing logic (handled by PaymentService consumers)

### User Flow

This is a system-to-system service. The primary interaction flow is:

1. **Service Startup:** HSM Interface Service initializes, opens PKCS#11 session to HSM, verifies Master Key presence, runs self-test diagnostics
2. **Client Authentication:** Calling service (e.g., PaymentService) establishes mTLS connection to HSM Interface Service
3. **Key Derivation Request:** Client requests a session key for a transaction — service derives from Zone Master Key within the HSM, returns encrypted key handle
4. **Cryptographic Operation:** Client requests PIN encryption, MAC generation, or token generation — service performs operation inside HSM boundary, returns result
5. **Audit Trail:** Every operation is logged with timestamp, operation type, key reference (never key material), caller identity, and success/failure status
6. **Error Handling:** If HSM is unreachable, circuit breaker opens, callers receive a clear error, and alerting fires

---

## Acceptance Criteria

- [ ] HSM Interface satellite service is running and accessible via gRPC with PKCS#11 interop to the HSM hardware (or SoftHSM2 in dev/test)
- [ ] Master Key generation is performed within the HSM boundary — key material never leaves the HSM
- [ ] Session key derivation produces unique keys per transaction using Zone Master Key and diversification data
- [ ] PIN block encryption produces ISO 9564 Format 0 compliant output given a cleartext PIN and account PAN
- [ ] PIN block decryption correctly recovers the PIN from an ISO 9564 Format 0 block using the correct zone key
- [ ] MAC generation produces ISO 9797-1 Algorithm 3 compliant MACs for switch message authentication
- [ ] MAC verification correctly validates or rejects MACs on inbound switch messages
- [ ] Every HSM operation (success and failure) is written to the audit log with: timestamp, operation_type, key_reference, caller_identity, result (success/fail), duration_ms
- [ ] All gRPC connections to the HSM service require valid mTLS client certificates — unauthenticated calls are rejected
- [ ] Circuit breaker opens after 3 consecutive HSM failures, with a 30-second half-open retry interval
- [ ] Health check endpoint reports HSM session status, key availability, and service readiness

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `UniBank.HSM` | `src/Satellites/UniBank.HSM/` | Satellite service project |
| `HSMService.cs` | `src/Satellites/UniBank.HSM/Services/` | gRPC service implementation |
| `Pkcs11Provider.cs` | `src/Satellites/UniBank.HSM/Crypto/` | PKCS#11 interop wrapper |
| `KeyHierarchy.cs` | `src/Satellites/UniBank.HSM/Crypto/` | Key derivation logic |
| `PinBlockCodec.cs` | `src/Satellites/UniBank.HSM/Crypto/` | ISO 9564 PIN block encoding/decoding |
| `MacGenerator.cs` | `src/Satellites/UniBank.HSM/Crypto/` | ISO 9797-1 MAC operations |
| `HsmCircuitBreaker.cs` | `src/Satellites/UniBank.HSM/Resilience/` | Polly circuit breaker for HSM |
| `AuditLogger.cs` | `src/Satellites/UniBank.HSM/Audit/` | Structured audit log writer |
| `hsm_service.proto` | `src/Shared/UniBank.Protos/` | gRPC proto definition |

### API / gRPC Endpoints

**Proto definition** (`hsm_service.proto`):

```protobuf
syntax = "proto3";
package unibank.hsm.v1;

service HSMService {
  rpc GenerateKey (GenerateKeyRequest) returns (GenerateKeyResponse);
  rpc DeriveSessionKey (DeriveSessionKeyRequest) returns (DeriveSessionKeyResponse);
  rpc EncryptPINBlock (EncryptPINBlockRequest) returns (EncryptPINBlockResponse);
  rpc DecryptPINBlock (DecryptPINBlockRequest) returns (DecryptPINBlockResponse);
  rpc GenerateMAC (GenerateMACRequest) returns (GenerateMACResponse);
  rpc VerifyMAC (VerifyMACRequest) returns (VerifyMACResponse);
  rpc GenerateToken (GenerateTokenRequest) returns (GenerateTokenResponse);
  rpc CheckHealth (HealthCheckRequest) returns (HealthCheckResponse);
}

message GenerateKeyRequest {
  string key_type = 1;           // "ZMK", "ZPK", "TMK", "TAK"
  string key_label = 2;          // Human-readable label
  int32 key_length_bits = 3;     // 128, 192, or 256
  string tenant_id = 4;
}

message GenerateKeyResponse {
  string key_reference = 1;      // HSM key handle/label — never raw material
  string key_check_value = 2;    // KCV for verification
  bool success = 3;
  string error_message = 4;
}

message DeriveSessionKeyRequest {
  string zone_master_key_ref = 1;
  bytes diversification_data = 2; // Transaction-unique data (e.g., ATC + terminal ID)
  string tenant_id = 3;
}

message DeriveSessionKeyResponse {
  string session_key_reference = 1;
  bytes encrypted_session_key = 2; // Session key encrypted under ZMK for transport
  bool success = 3;
  string error_message = 4;
}

message EncryptPINBlockRequest {
  string pin = 1;                // Cleartext PIN (transported over mTLS only)
  string pan = 2;                // Account PAN for Format 0
  string zone_pin_key_ref = 3;   // ZPK reference
  string tenant_id = 4;
}

message EncryptPINBlockResponse {
  bytes pin_block = 1;           // ISO 9564 Format 0 encrypted PIN block
  bool success = 2;
  string error_message = 3;
}

message DecryptPINBlockRequest {
  bytes pin_block = 1;
  string pan = 2;
  string zone_pin_key_ref = 3;
  string tenant_id = 4;
}

message DecryptPINBlockResponse {
  string pin = 1;                // Recovered cleartext PIN
  bool success = 2;
  string error_message = 3;
}

message GenerateMACRequest {
  bytes message_data = 1;        // Raw switch message bytes
  string tak_reference = 2;      // Terminal Authentication Key ref
  string tenant_id = 3;
}

message GenerateMACResponse {
  bytes mac = 1;                 // 8-byte MAC
  bool success = 2;
  string error_message = 3;
}

message VerifyMACRequest {
  bytes message_data = 1;
  bytes mac = 2;
  string tak_reference = 3;
  string tenant_id = 4;
}

message VerifyMACResponse {
  bool is_valid = 1;
  bool success = 2;
  string error_message = 3;
}

message GenerateTokenRequest {
  string account_pan = 1;
  string device_id = 2;
  string tenant_id = 3;
  int32 token_expiry_days = 4;   // Default: 365
}

message GenerateTokenResponse {
  string token_pan = 1;          // Token PAN replacing real PAN
  string token_reference = 2;
  int64 expires_at_unix = 3;
  bool success = 4;
  string error_message = 5;
}
```

### Database Changes

**Audit log table** (in the HSM satellite's own schema):

```sql
CREATE TABLE hsm_audit_log (
    id              BIGSERIAL PRIMARY KEY,
    timestamp       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    operation_type  VARCHAR(50) NOT NULL,  -- GenerateKey, DeriveSessionKey, EncryptPINBlock, etc.
    key_reference   VARCHAR(100),          -- HSM key label/handle used
    caller_identity VARCHAR(200) NOT NULL, -- mTLS CN of the calling service
    tenant_id       VARCHAR(50),
    result          VARCHAR(10) NOT NULL,  -- 'success' or 'fail'
    error_code      VARCHAR(50),
    duration_ms     INT NOT NULL,
    metadata        JSONB                  -- Additional context (non-sensitive)
);

CREATE INDEX idx_hsm_audit_timestamp ON hsm_audit_log (timestamp DESC);
CREATE INDEX idx_hsm_audit_operation ON hsm_audit_log (operation_type, result);
CREATE INDEX idx_hsm_audit_tenant ON hsm_audit_log (tenant_id, timestamp DESC);
```

**Key registry table** (tracks keys generated in the HSM):

```sql
CREATE TABLE hsm_key_registry (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key_reference   VARCHAR(100) NOT NULL UNIQUE, -- HSM key handle/label
    key_type        VARCHAR(20) NOT NULL,          -- ZMK, ZPK, TMK, TAK
    key_length_bits INT NOT NULL,
    key_check_value VARCHAR(16) NOT NULL,          -- KCV hex
    tenant_id       VARCHAR(50) NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'active', -- active, rotated, destroyed
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    rotated_at      TIMESTAMPTZ,
    destroyed_at    TIMESTAMPTZ
);
```

### Security Considerations

- **PKCS#11 Interop:** Use `PKCS11Interop` NuGet package (MIT licensed) for managed .NET access to HSM via PKCS#11 v2.40. Alternatively, raw P/Invoke to the vendor's PKCS#11 `.so` / `.dll` library. The interop layer must handle slot enumeration, session management, and mechanism selection.
- **Key Hierarchy:**
  - **Master Key (MK):** Generated and stored exclusively inside the HSM. Never exported. Used to encrypt Zone Master Keys at rest.
  - **Zone Master Keys (ZMK):** Derived from MK per zone/function. Types: Zone PIN Key (ZPK) for PIN operations, Terminal Master Key (TMK) for terminal key exchange, Terminal Authentication Key (TAK) for MAC operations.
  - **Session Keys:** Derived from ZMK using diversification data unique to each transaction. Short-lived — one session key per transaction.
- **PIN Block Format:** ISO 9564-1 Format 0 (ISO 0). PIN block = PIN field XOR PAN field. PIN field: `0 | PIN_length | PIN_digits | F_padding`. PAN field: `0000 | rightmost_12_PAN_digits_excluding_check`.
- **MAC Algorithm:** ISO 9797-1 Algorithm 3 (Retail MAC). Uses double-length TDES key. Process: CBC-MAC with left half of key, then decrypt last block with right half, re-encrypt with left half.
- **mTLS Enforcement:** The gRPC server must be configured with `SslServerCredentials` requiring client certificates. The CA that signs client certs is pre-shared during deployment. Reject any connection without a valid client certificate.
- **No Key Material in Logs:** Audit logs must never contain raw key material, PINs, or PIN blocks. Only key references (labels/handles), operation types, and results.
- **SoftHSM2 for Development:** Use SoftHSM2 (OASIS reference implementation) for local development and CI. Configuration via `softhsm2.conf`. Ensures functional parity without physical HSM hardware.
- **Key Rotation:** Keys support rotation — generate new key, re-encrypt under new key, mark old key as `rotated`. Grace period allows both old and new key to be active during transition.

### Edge Cases

- **HSM Unreachable:** Circuit breaker (Polly) opens after 3 consecutive failures. Returns `Unavailable` gRPC status. Half-open state retries after 30 seconds. Alert fires on circuit open.
- **HSM Session Timeout:** PKCS#11 sessions can time out. The interop layer must detect `CKR_SESSION_HANDLE_INVALID` and automatically re-establish the session.
- **Concurrent Access:** PKCS#11 sessions are not thread-safe. Use a session pool — each concurrent operation gets its own session from the pool. Pool size configurable per environment.
- **Key Not Found:** If a requested key reference does not exist in the HSM, return a clear error with the key reference (not a generic crypto error). Log at WARN level.
- **Invalid PIN Length:** PINs must be 4-12 digits per ISO 9564. Reject PINs outside this range before touching the HSM.
- **Tenant Isolation:** Each tenant's keys are prefixed with tenant ID in the HSM label (e.g., `TENANT01_ZPK_001`). Operations must validate that the caller's tenant matches the key's tenant.
- **HSM Failover:** If using networked HSM with primary/secondary, implement failover at the PKCS#11 slot level. Primary slot first, secondary on failure.
- **Clock Skew:** Audit log timestamps use server UTC time. Ensure NTP synchronization on HSM service hosts.

---

## Dependencies

**Prerequisite Stories:**
- STORY-004: gRPC Proto Definitions — proto files for HSMService must be defined

**Blocked Stories:**
- STORY-022: NFC Payment Tokenization (requires GenerateToken)
- STORY-023: NFC Contactless Payment at POS (requires DeriveSessionKey, EncryptPINBlock)
- STORY-024: PIN Entry for High-Value NFC (requires DecryptPINBlock, DeriveSessionKey)
- STORY-026: Generate EMV QR Code (indirect — QR payment authorization uses HSM)

**External Dependencies:**
- Physical HSM hardware (Thales Luna, Utimaco, or equivalent) for staging/production
- SoftHSM2 package for development and CI environments
- PKCS#11 vendor library (`.so` / `.dll`) for the specific HSM model
- mTLS certificates issued by the deployment CA

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — crypto operations tested against known test vectors
- [ ] Integration tests passing with SoftHSM2
- [ ] mTLS authentication enforced and tested (unauthenticated calls rejected)
- [ ] Circuit breaker tested (HSM unavailability handled gracefully)
- [ ] Audit logging verified — all operations produce correct audit records
- [ ] Key hierarchy established and verified (MK -> ZMK -> Session Key)
- [ ] PIN block tested against ISO 9564 Format 0 test vectors
- [ ] MAC tested against ISO 9797-1 Algorithm 3 test vectors
- [ ] Code reviewed and approved
- [ ] Documentation updated (API docs, operational runbook for key ceremony)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging with SoftHSM2

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
