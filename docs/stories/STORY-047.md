# STORY-047: Terminal Key Management via HSM

**Epic:** EPIC-009 Terminal Management & HSM
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 6

---

## User Story

As a **system**
I want **terminal encryption keys managed through HSM**
So that **PIN data is always securely encrypted and key material never exists in plaintext outside the HSM boundary**

---

## Description

### Background

Every time a customer enters a PIN on an EFT POS terminal, that PIN must be encrypted using keys that are securely derived, distributed, and rotated. In Southern African banking regulations and PCI PIN Security requirements, terminal key management is not optional — it is a compliance gate. A compromised terminal key can expose thousands of customer PINs, making this one of the highest-security components in the UniBank platform.

UniBank implements a three-tier key hierarchy for terminal cryptography:
- **Terminal Master Key (TMK):** The root key for each terminal. Generated inside the HSM, never leaves the HSM boundary in cleartext. Used to encrypt session keys for transport to the terminal.
- **Terminal PIN Key (TPK):** Derived from the TMK. Used by the terminal to encrypt PIN blocks during cardholder verification. Rotated on a configurable schedule.
- **Terminal MAC Key (TMK-MAC):** Derived from the TMK. Used to generate Message Authentication Codes on transaction messages between the terminal and the switch, ensuring message integrity.

Key distribution to terminals happens over MQTT, but the keys themselves are encrypted under the terminal's transport key before transmission. Even if the MQTT channel were compromised, the intercepted key material would be useless without access to the HSM and the terminal's secure element.

This story implements the full key lifecycle: generation, distribution, activation, rotation, and archival — all orchestrated between the Terminal Manager satellite service and the HSM Interface satellite service (STORY-021).

**Functional Requirement:** FR-033

### Scope

**In scope:**
- Terminal Master Key (TMK) generation inside HSM per terminal
- Terminal PIN Key (TPK) derivation from TMK within the HSM
- Terminal MAC Key (TMK-MAC) derivation from TMK within the HSM
- Encrypted key distribution to terminals via MQTT topic `terminals/{id}/keys`
- Key rotation on configurable schedule (daily/weekly) and on-demand rotation
- Key lifecycle management: generated -> distributed -> active -> expired -> archived
- Key status tracking in `terminal_keys` database table
- Comprehensive audit logging of all key operations
- Key rollback mechanism for failed key injection
- Grace period support during key rotation (old and new keys both valid temporarily)

**Out of scope:**
- HSM hardware initialization and master key ceremony (operational procedure)
- PKCS#11 interop implementation (covered in STORY-021)
- Terminal-side key storage implementation (terminal vendor responsibility)
- Key escrow or key recovery procedures (separate operational story)
- Cross-tenant key sharing (explicitly forbidden)

### User Flow

This is a system-to-system flow triggered during terminal provisioning (STORY-046) or scheduled key rotation:

**Initial Key Injection (during provisioning):**
1. Terminal completes MQTT connection and receives configuration (STORY-046)
2. Terminal Manager requests TMK generation from HSM Interface via `HSMService.GenerateKey` (key_type: "TMK", bound to terminal ID)
3. HSM generates TMK inside the secure boundary and returns the key reference (handle) and Key Check Value (KCV)
4. Terminal Manager requests TPK derivation from HSM Interface via `HSMService.DeriveSessionKey` using TMK as the zone master key
5. HSM derives TPK inside the secure boundary, encrypts it under the terminal's transport key, returns encrypted TPK
6. Terminal Manager requests TMK-MAC derivation similarly
7. Terminal Manager publishes encrypted TPK and TMK-MAC to `terminals/{terminal_id}/keys` via MQTT
8. Terminal receives keys, loads them into its secure element, verifies KCV, and acknowledges on `terminals/{terminal_id}/status`
9. Terminal Manager verifies acknowledgment, updates key status to `active`, and updates terminal status to `active`
10. Audit log records the full key injection chain

**Scheduled Key Rotation:**
1. Background job (Wolverine scheduled message) fires based on the tenant's configured rotation schedule
2. For each terminal due for rotation, the system derives new TPK and TMK-MAC from the existing TMK
3. New keys are distributed via MQTT with a key version number
4. Terminal loads new keys alongside existing keys (grace period)
5. Terminal acknowledges new keys — new keys become primary, old keys enter grace period
6. After grace period expires (configurable, default 1 hour), old keys are marked `expired`

---

## Acceptance Criteria

- [ ] Terminal Master Key (TMK) is generated inside the HSM — key material never leaves the HSM boundary in cleartext
- [ ] Terminal PIN Key (TPK) is derived from TMK within the HSM and encrypted under the terminal's transport key before distribution
- [ ] Terminal MAC Key (TMK-MAC) is derived from TMK within the HSM and encrypted under the terminal's transport key before distribution
- [ ] Encrypted keys are distributed to terminals via MQTT topic `terminals/{terminal_id}/keys`
- [ ] Key rotation executes on a configurable schedule (daily or weekly, per tenant configuration)
- [ ] On-demand key rotation can be triggered for a specific terminal via `TerminalKeyService.RotateTerminalKeys` gRPC endpoint
- [ ] During key rotation, a grace period allows both old and new keys to remain valid (configurable duration, default 1 hour)
- [ ] All key operations are logged in the audit trail with: timestamp, operation_type, terminal_id, key_type, key_reference (never key material), result, operator identity
- [ ] Key lifecycle states are tracked: `generated` -> `distributed` -> `active` -> `expired` -> `archived`
- [ ] Failed key injection (terminal does not acknowledge within timeout) is detected, logged, and the key status is set to `distribution_failed`
- [ ] Key Check Values (KCV) are used to verify key integrity after distribution — terminal reports KCV, system validates against HSM-generated KCV
- [ ] Each tenant's terminal keys are isolated — keys generated for Tenant A's terminals cannot be used by Tenant B's terminals

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `TerminalKeyService.cs` | `src/Satellites/UniBank.TerminalManager/Services/` | gRPC service for terminal key operations |
| `KeyDistributionHandler.cs` | `src/Satellites/UniBank.TerminalManager/Handlers/` | Wolverine handler orchestrating key distribution via MQTT |
| `KeyRotationScheduler.cs` | `src/Satellites/UniBank.TerminalManager/Jobs/` | Background job for scheduled key rotation |
| `KeyAcknowledgmentHandler.cs` | `src/Satellites/UniBank.TerminalManager/Handlers/` | Handles terminal key acknowledgment messages |
| `TerminalKeyEntity.cs` | `src/Satellites/UniBank.TerminalManager/Domain/` | Terminal key entity |
| `KeyRotationDueEvent.cs` | `src/Shared/UniBank.Events/Terminal/` | Wolverine event for key rotation scheduling |
| `KeyInjectionFailedEvent.cs` | `src/Shared/UniBank.Events/Terminal/` | Wolverine event for key injection failure alerting |

### API / gRPC Endpoints

**Additions to `terminal_service.proto`:**

```protobuf
service TerminalKeyService {
  rpc InjectTerminalKeys (InjectKeysRequest) returns (InjectKeysResponse);
  rpc RotateTerminalKeys (RotateKeysRequest) returns (RotateKeysResponse);
  rpc GetTerminalKeyStatus (GetKeyStatusRequest) returns (GetKeyStatusResponse);
  rpc RevokeTerminalKeys (RevokeKeysRequest) returns (RevokeKeysResponse);
}

message InjectKeysRequest {
  int32 terminal_id = 1;
  string tenant_id = 2;
}

message InjectKeysResponse {
  string tmk_reference = 1;       // HSM key handle for TMK
  string tmk_kcv = 2;             // Key Check Value for TMK
  string tpk_reference = 3;       // HSM key handle for TPK
  string tpk_kcv = 4;             // Key Check Value for TPK
  string tmk_mac_reference = 5;   // HSM key handle for TMK-MAC
  string tmk_mac_kcv = 6;         // Key Check Value for TMK-MAC
  bool success = 7;
  string error_message = 8;
}

message RotateKeysRequest {
  int32 terminal_id = 1;
  repeated string key_types = 2;  // "TPK", "TMK_MAC", or both. TMK rotation requires re-inject.
  string tenant_id = 3;
}

message RotateKeysResponse {
  repeated KeyRotationResult results = 1;
  bool success = 2;
  string error_message = 3;
}

message KeyRotationResult {
  string key_type = 1;
  string new_key_reference = 2;
  string new_kcv = 3;
  string old_key_reference = 4;
  string old_key_status = 5;      // "grace_period"
  int64 grace_period_expires_unix = 6;
}

message GetKeyStatusRequest {
  int32 terminal_id = 1;
  string tenant_id = 2;
}

message GetKeyStatusResponse {
  repeated TerminalKeyInfo keys = 1;
}

message TerminalKeyInfo {
  string key_type = 1;
  string key_reference = 2;
  string status = 3;
  string kcv = 4;
  int64 generated_at_unix = 5;
  int64 expires_at_unix = 6;
  int64 rotated_at_unix = 7;
}
```

### MQTT Key Distribution

**Key injection payload** (published to `terminals/{terminal_id}/keys`):

```json
{
  "operation": "key_inject",
  "key_version": 1,
  "keys": [
    {
      "key_type": "TPK",
      "encrypted_key_block": "BASE64_ENCODED_ENCRYPTED_TPK",
      "kcv": "A1B2C3",
      "algorithm": "3DES",
      "key_length_bits": 128
    },
    {
      "key_type": "TMK_MAC",
      "encrypted_key_block": "BASE64_ENCODED_ENCRYPTED_TMK_MAC",
      "kcv": "D4E5F6",
      "algorithm": "3DES",
      "key_length_bits": 128
    }
  ],
  "ack_required": true,
  "ack_timeout_seconds": 120,
  "timestamp": "2026-02-24T10:05:00Z"
}
```

**Key acknowledgment** (terminal publishes to `terminals/{terminal_id}/status`):

```json
{
  "message_type": "key_ack",
  "key_version": 1,
  "results": [
    { "key_type": "TPK", "kcv_verified": true, "loaded": true },
    { "key_type": "TMK_MAC", "kcv_verified": true, "loaded": true }
  ],
  "timestamp": "2026-02-24T10:05:02Z"
}
```

### Database Changes

**terminal_keys table** (in `terminal_manager` schema):

```sql
CREATE TABLE terminal_manager.terminal_keys (
    id              SERIAL PRIMARY KEY,
    tenant_id       VARCHAR(50) NOT NULL,
    terminal_id     INT NOT NULL REFERENCES terminal_manager.terminals(id),
    key_type        VARCHAR(20) NOT NULL CHECK (key_type IN ('TMK', 'TPK', 'TMK_MAC')),
    key_reference   VARCHAR(100) NOT NULL,  -- HSM key handle (never raw material)
    key_version     INT NOT NULL DEFAULT 1,
    kcv             VARCHAR(16) NOT NULL,    -- Key Check Value for verification
    status          VARCHAR(20) NOT NULL DEFAULT 'generated'
                    CHECK (status IN ('generated', 'distributed', 'active', 'grace_period',
                                      'expired', 'archived', 'distribution_failed', 'revoked')),
    generated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    distributed_at  TIMESTAMPTZ,
    activated_at    TIMESTAMPTZ,
    expires_at      TIMESTAMPTZ,
    rotated_at      TIMESTAMPTZ,
    archived_at     TIMESTAMPTZ,
    UNIQUE (terminal_id, key_type, key_version)
);

CREATE INDEX idx_terminal_keys_terminal ON terminal_manager.terminal_keys (terminal_id, key_type);
CREATE INDEX idx_terminal_keys_status ON terminal_manager.terminal_keys (status)
    WHERE status IN ('active', 'grace_period');
CREATE INDEX idx_terminal_keys_rotation ON terminal_manager.terminal_keys (expires_at)
    WHERE status = 'active';
CREATE INDEX idx_terminal_keys_tenant ON terminal_manager.terminal_keys (tenant_id);
```

### Security Considerations

- **Key Hierarchy Enforcement:** TMK is the root of trust per terminal. TPK and TMK-MAC are always derived from TMK — never generated independently. This ensures that compromising a session key does not compromise the master key.
- **Transport Encryption:** Keys distributed via MQTT are encrypted under the terminal's transport key (derived from TMK during initial key exchange). Even with TLS on the MQTT channel, defense-in-depth dictates application-layer encryption of key material.
- **Key Check Value Verification:** After key injection, the terminal computes the KCV of the loaded key (encrypt a zero block, take first 3 bytes) and reports it back. The system compares this against the HSM-generated KCV. Mismatch triggers a key injection failure.
- **Tenant Key Isolation:** TMKs are generated with the tenant ID embedded in the key label (e.g., `TENANT01_TMK_TERM12345`). The HSM Interface enforces that key operations on a key reference must match the caller's tenant context.
- **No Plaintext Keys in Logs:** Audit logs record key references and KCVs only — never raw key material, encrypted key blocks, or any data from which key material could be derived.
- **Key Rotation Grace Period:** During rotation, both old and new keys are valid. This prevents transaction failures during the brief window when some messages might be encrypted with the old key while the new key is being loaded.
- **HSM Session Security:** All key operations go through the HSM Interface satellite service over mTLS. The Terminal Manager never has direct PKCS#11 access to the HSM.

### Edge Cases

- **Terminal offline during key injection:** If the terminal does not acknowledge within the timeout (default 120 seconds), the key status is set to `distribution_failed`. Retry is automatic on the next terminal connection.
- **KCV mismatch after injection:** If the terminal reports a KCV that does not match the HSM-generated KCV, the key is marked `distribution_failed`, an alert fires, and the terminal is suspended pending investigation (possible tampering indicator).
- **Key rotation during active transaction:** The grace period ensures that transactions started with the old key can complete. The terminal should complete in-flight transactions before switching primary keys.
- **HSM unavailable during rotation:** If the HSM Interface is unavailable when rotation is due, the scheduler retries with exponential backoff. Keys remain valid beyond their scheduled rotation time — extended validity is logged as a compliance event.
- **TMK compromise:** If a TMK is suspected compromised, the `RevokeTerminalKeys` endpoint marks all keys for that terminal as `revoked`, publishes a key wipe command via MQTT, and suspends the terminal. A new TMK must be generated and the terminal re-provisioned.
- **Concurrent rotation requests:** The system uses optimistic concurrency (key version number) to prevent duplicate rotations. If two rotation requests arrive simultaneously, only one succeeds; the other receives a conflict error.
- **Orphaned keys:** Background cleanup job archives keys that have been in `expired` status for longer than the retention period (default 90 days).

---

## Dependencies

**Prerequisite Stories:**
- STORY-046: Terminal Registration & Provisioning — terminals must be registered before keys can be injected
- STORY-021: HSM Interface Service — HSM operations (GenerateKey, DeriveSessionKey) must be available

**Blocked Stories:**
- Terminal payment processing (NFC, QR) — terminals cannot process PIN-based transactions without active TPK
- STORY-048: Terminal Status Monitoring — key status is a component of terminal health

**External Dependencies:**
- HSM hardware (or SoftHSM2 for dev/test) must be operational and accessible by HSM Interface Service
- MQTT broker with TLS for secure key distribution channel
- Terminal firmware must support the key injection protocol (key block format, KCV verification, acknowledgment)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — key lifecycle state machine, distribution logic, rotation scheduling
- [ ] Integration tests passing with SoftHSM2 and MQTT broker — full key injection and rotation flow
- [ ] Key Check Value verification tested — correct KCV accepted, mismatched KCV triggers failure
- [ ] Key rotation schedule tested — daily and weekly rotation confirmed
- [ ] Grace period tested — both old and new keys accepted during grace period, old keys rejected after expiry
- [ ] Audit logging verified — all key operations produce correct audit records with no plaintext key material
- [ ] Tenant isolation verified — keys for Tenant A inaccessible from Tenant B context
- [ ] Code reviewed and approved
- [ ] Documentation updated (key management procedures, rotation schedule configuration)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
