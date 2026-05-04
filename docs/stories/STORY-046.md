# STORY-046: Terminal Registration & Provisioning

**Epic:** EPIC-009 Terminal Management & HSM
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 6

---

## User Story

As an **admin**
I want to **register and provision EFT POS terminals**
So that **merchants can accept digital payments**

---

## Description

### Background

GoldBank's value proposition to deploying institutions in Southern Africa hinges on enabling digital payments at the point of sale. EFT POS terminals are the physical bridge between the platform and real-world commerce — they process NFC taps, QR scans, and PIN entry for card-present transactions. Before a terminal can process any payment, it must be registered in the system, associated with a merchant, provisioned with configuration, and activated through a secure key exchange with the HSM.

Terminal provisioning in GoldBank follows a pull-then-push model: an administrator registers the terminal in the system (creating the record and merchant association), and when the terminal powers on and connects via MQTT, the platform pushes configuration and initiates the cryptographic key exchange that ultimately activates the device. This zero-touch provisioning model is essential for deploying institutions operating in areas where on-site technical support is scarce — once a terminal is registered by an admin, a merchant can power it on and it self-provisions.

The Terminal Manager is a satellite service in the GoldBank architecture, communicating with POS terminals over MQTT (lightweight, suitable for intermittent connectivity common in Southern African deployments) and with the core platform via gRPC.

**Functional Requirement:** FR-032

### Scope

**In scope:**
- Admin-initiated terminal registration via gRPC endpoint
- Terminal record creation with unique device ID and merchant association
- MQTT-based configuration push to terminals on connection
- Terminal provisioning flow: register, connect, configure, key exchange, activate
- MQTT topic structure for terminal communication (`terminals/{id}/config`, `terminals/{id}/status`, `terminals/{id}/commands`)
- Terminal status lifecycle: `registered` -> `active` -> `suspended` -> `decommissioned`
- Audit trail for all registration and provisioning events
- Validation of terminal uniqueness (no duplicate device IDs)
- Bulk terminal registration for merchant fleet deployments

**Out of scope:**
- HSM key exchange implementation (covered in STORY-047)
- Terminal heartbeat monitoring (covered in STORY-048)
- Remote software update mechanism (covered in STORY-049)
- Physical terminal hardware procurement
- Terminal hardware certification (EMV L1/L2)
- Merchant onboarding workflow (prerequisite — merchant must already exist)

### User Flow

1. **Admin registers terminal:** Admin uses the admin portal or gRPC API to create a terminal record — provides terminal device ID (from hardware label), model, merchant ID, and optional initial configuration overrides
2. **System validates and creates record:** System validates the terminal device ID is unique, the merchant exists, and creates the terminal record with status `registered`. An audit log entry is written.
3. **Terminal powers on at merchant site:** The physical terminal boots and connects to the MQTT broker using its embedded device credentials (pre-loaded at factory or via QR-code bootstrap)
4. **Terminal subscribes to its topics:** Terminal subscribes to `terminals/{terminal_id}/config`, `terminals/{terminal_id}/commands`, and publishes to `terminals/{terminal_id}/status`
5. **Terminal Manager detects new connection:** The Terminal Manager satellite service (subscribed to `terminals/+/status`) receives the terminal's initial status message. It looks up the terminal record and confirms status is `registered`.
6. **Configuration pushed:** Terminal Manager publishes the terminal's configuration payload to `terminals/{terminal_id}/config`. This includes merchant details, transaction parameters, network configuration, and the HSM endpoint for key exchange.
7. **Terminal initiates key exchange:** Terminal uses the HSM endpoint from config to initiate key exchange (STORY-047). The Terminal Master Key (TMK), Terminal PIN Key (TPK), and Terminal MAC Key (TMK-MAC) are injected.
8. **Activation:** On successful key exchange, the Terminal Manager updates the terminal status to `active`. The terminal is now ready to process payments.
9. **Confirmation:** Admin sees the terminal status change to `active` in the admin portal. Audit log records the full provisioning chain.

---

## Acceptance Criteria

- [ ] Admin can register a terminal with a unique device ID and assign it to an existing merchant via `TerminalService.RegisterTerminal` gRPC endpoint
- [ ] Registration rejects duplicate terminal device IDs with a clear error message
- [ ] Registration rejects requests referencing non-existent merchants
- [ ] Terminal configuration is pushed to the terminal via MQTT topic `terminals/{terminal_id}/config` when the terminal connects
- [ ] Configuration payload includes: merchant details, transaction parameters, HSM endpoint, network settings, and terminal-specific overrides
- [ ] Terminal activation requires a successful key exchange with the HSM Interface (status transitions from `registered` to `active` only after key exchange confirmation)
- [ ] Terminal status lifecycle is enforced: `registered` -> `active` -> `suspended` -> `decommissioned` (no skipping states, except `registered` -> `decommissioned` for cancellation)
- [ ] All registration, provisioning, and activation events are logged in the audit trail with: timestamp, admin user, terminal ID, merchant ID, action, result
- [ ] Bulk registration endpoint accepts up to 100 terminals per request and returns individual success/failure per terminal
- [ ] Terminal configuration is tenant-scoped — terminals can only be registered within the admin's tenant context

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `GoldBank.TerminalManager` | `src/Satellites/GoldBank.TerminalManager/` | Terminal Manager satellite service |
| `TerminalService.cs` | `src/Satellites/GoldBank.TerminalManager/Services/` | gRPC service for terminal operations |
| `MqttTerminalBridge.cs` | `src/Satellites/GoldBank.TerminalManager/Mqtt/` | MQTT publish/subscribe handler for terminal communication |
| `TerminalProvisioningHandler.cs` | `src/Satellites/GoldBank.TerminalManager/Handlers/` | Wolverine handler for provisioning workflow orchestration |
| `TerminalRegisteredEvent.cs` | `src/Shared/GoldBank.Events/Terminal/` | Domain event for terminal registration |
| `TerminalActivatedEvent.cs` | `src/Shared/GoldBank.Events/Terminal/` | Domain event for terminal activation |
| `terminal_service.proto` | `src/Shared/GoldBank.Protos/` | gRPC proto definition for TerminalService |
| `Terminal.cs` | `src/Satellites/GoldBank.TerminalManager/Domain/` | Terminal entity |
| `TerminalConfiguration.cs` | `src/Satellites/GoldBank.TerminalManager/Domain/` | Configuration value object |

### API / gRPC Endpoints

**Proto definition** (`terminal_service.proto`):

```protobuf
syntax = "proto3";
package goldbank.terminal.v1;

service TerminalService {
  rpc RegisterTerminal (RegisterTerminalRequest) returns (RegisterTerminalResponse);
  rpc RegisterTerminalBatch (RegisterTerminalBatchRequest) returns (RegisterTerminalBatchResponse);
  rpc GetTerminal (GetTerminalRequest) returns (GetTerminalResponse);
  rpc GetAllTerminals (GetAllTerminalsRequest) returns (GetAllTerminalsResponse);
  rpc UpdateTerminalConfig (UpdateTerminalConfigRequest) returns (UpdateTerminalConfigResponse);
  rpc SuspendTerminal (SuspendTerminalRequest) returns (SuspendTerminalResponse);
  rpc DecommissionTerminal (DecommissionTerminalRequest) returns (DecommissionTerminalResponse);
  rpc ReactivateTerminal (ReactivateTerminalRequest) returns (ReactivateTerminalResponse);
}

message RegisterTerminalRequest {
  string terminal_device_id = 1;  // Unique hardware device identifier
  string merchant_id = 2;         // Merchant this terminal is assigned to
  string model = 3;               // Terminal model (e.g., "PAX A920", "Ingenico Move5000")
  string firmware_version = 4;    // Initial firmware version
  string tenant_id = 5;
  map<string, string> config_overrides = 6; // Optional config overrides
}

message RegisterTerminalResponse {
  int32 terminal_id = 1;          // System-assigned terminal ID
  string terminal_device_id = 2;
  string status = 3;              // "registered"
  bool success = 4;
  string error_message = 5;
}

message RegisterTerminalBatchRequest {
  repeated RegisterTerminalRequest terminals = 1;
  string tenant_id = 2;
}

message RegisterTerminalBatchResponse {
  repeated RegisterTerminalBatchResult results = 1;
  int32 success_count = 2;
  int32 failure_count = 3;
}

message RegisterTerminalBatchResult {
  string terminal_device_id = 1;
  int32 terminal_id = 2;
  bool success = 3;
  string error_message = 4;
}

message GetTerminalRequest {
  int32 terminal_id = 1;
  string tenant_id = 2;
}

message GetTerminalResponse {
  int32 terminal_id = 1;
  string terminal_device_id = 2;
  string merchant_id = 3;
  string model = 4;
  string firmware_version = 5;
  string status = 6;
  string config_json = 7;
  int64 last_heartbeat_unix = 8;
  int64 created_at_unix = 9;
}
```

### MQTT Topic Structure

| Topic | Direction | Purpose |
|-------|-----------|---------|
| `terminals/{terminal_id}/config` | Platform -> Terminal | Push configuration to terminal |
| `terminals/{terminal_id}/status` | Terminal -> Platform | Heartbeat and status updates |
| `terminals/{terminal_id}/commands` | Platform -> Terminal | Command channel (restart, diagnostics, etc.) |
| `terminals/+/status` | Platform subscribes | Wildcard subscription for all terminal status updates |

**Configuration payload** (published to `terminals/{terminal_id}/config`):

```json
{
  "terminal_id": 12345,
  "terminal_device_id": "PAX-A920-SN00012345",
  "merchant_id": "MERCH-001",
  "merchant_name": "Shoprite Mabopane",
  "tenant_id": "TENANT-ZA-001",
  "hsm_endpoint": "hsm.internal:5001",
  "transaction_config": {
    "nfc_enabled": true,
    "qr_enabled": true,
    "pin_threshold_amount": 500.00,
    "currency_code": "ZAR",
    "max_transaction_amount": 50000.00,
    "offline_transaction_limit": 5,
    "offline_amount_limit": 2500.00
  },
  "network_config": {
    "mqtt_broker": "mqtt.internal:8883",
    "mqtt_keepalive_seconds": 60,
    "heartbeat_interval_seconds": 60,
    "tls_required": true
  },
  "config_version": "2026-02-24T10:00:00Z"
}
```

### Database Changes

**terminals table** (in `terminal_manager` schema):

```sql
CREATE TABLE terminal_manager.terminals (
    id                  SERIAL PRIMARY KEY,
    tenant_id           VARCHAR(50) NOT NULL,
    merchant_id         VARCHAR(50) NOT NULL,
    terminal_device_id  VARCHAR(100) NOT NULL UNIQUE,
    model               VARCHAR(100) NOT NULL,
    firmware_version    VARCHAR(50),
    status              VARCHAR(20) NOT NULL DEFAULT 'registered'
                        CHECK (status IN ('registered', 'active', 'suspended', 'decommissioned')),
    config_json         JSONB,
    last_heartbeat      TIMESTAMPTZ,
    activated_at        TIMESTAMPTZ,
    suspended_at        TIMESTAMPTZ,
    decommissioned_at   TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_terminals_tenant ON terminal_manager.terminals (tenant_id);
CREATE INDEX idx_terminals_merchant ON terminal_manager.terminals (merchant_id);
CREATE INDEX idx_terminals_device_id ON terminal_manager.terminals (terminal_device_id);
CREATE INDEX idx_terminals_status ON terminal_manager.terminals (tenant_id, status);
CREATE INDEX idx_terminals_heartbeat ON terminal_manager.terminals (last_heartbeat)
    WHERE status = 'active';
```

### Security Considerations

- **Device Authentication:** Terminals authenticate to the MQTT broker using client certificates or pre-shared device credentials. The MQTT broker must verify terminal identity before allowing topic subscriptions.
- **MQTT TLS:** All MQTT communication uses TLS 1.2+ (port 8883). Plaintext MQTT (port 1883) is disabled in all environments.
- **Topic Authorization:** Terminals can only subscribe to and publish on their own topics (`terminals/{their_id}/*`). The MQTT broker enforces topic-level ACLs.
- **Tenant Isolation:** Terminal registration is scoped to the admin's tenant. A terminal registered in Tenant A cannot be queried or managed from Tenant B's context.
- **Config Sensitivity:** Configuration payloads pushed via MQTT do not contain key material. Key injection is a separate flow (STORY-047) with its own security model.
- **Audit Trail:** All provisioning actions are recorded with the admin's identity, ensuring accountability for terminal fleet management.
- **Device ID Validation:** Terminal device IDs are validated against a format pattern (alphanumeric with hyphens, 10-50 characters) to prevent injection attacks.

### Edge Cases

- **Terminal connects before registration:** If a terminal publishes to `terminals/{id}/status` but no record exists, the Terminal Manager ignores the message and logs a warning. The terminal retries on a backoff schedule.
- **Terminal connects after decommission:** If a decommissioned terminal connects, the Terminal Manager publishes a `decommission` command to `terminals/{id}/commands`, instructing the terminal to wipe keys and enter factory reset mode.
- **Duplicate device ID:** Registration rejects duplicates. If a replacement terminal has the same device ID, the admin must first decommission the old record.
- **Merchant deletion with active terminals:** Merchant deletion should be blocked if active terminals are assigned. Terminals must be decommissioned or reassigned first.
- **MQTT broker unavailable:** If the MQTT broker is down when a terminal is registered, the configuration push is queued and retried. The terminal record is created with status `registered` regardless — config push happens asynchronously.
- **Partial batch registration:** In bulk registration, each terminal is processed independently. Individual failures (duplicate ID, invalid merchant) do not roll back successful registrations in the same batch.
- **Network partition during provisioning:** If the terminal loses connectivity between config push and key exchange, the terminal retains status `registered`. On reconnection, the Terminal Manager detects the incomplete provisioning and re-pushes configuration.

---

## Dependencies

**Prerequisite Stories:**
- STORY-007: MQTT Infrastructure Setup — MQTT broker must be operational with TLS and topic ACLs
- STORY-021: HSM Interface Service — HSM must be available for the key exchange step of provisioning

**Blocked Stories:**
- STORY-047: Terminal Key Management via HSM (requires terminal registration to exist)
- STORY-048: Terminal Status Monitoring (requires terminal records and MQTT topics)
- STORY-049: Remote Terminal Software Updates (requires terminal records and MQTT topics)
- Future payment processing stories that depend on active terminals

**External Dependencies:**
- MQTT broker (Mosquitto or EMQX) deployed and configured with TLS and ACLs
- EFT POS terminal hardware or terminal emulator for integration testing
- SoftHSM2 for development/test key exchange flows

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — registration validation, status transitions, config generation
- [ ] Integration tests passing — full provisioning flow with MQTT broker and SoftHSM2
- [ ] MQTT topic publish/subscribe verified with terminal emulator
- [ ] Bulk registration tested with 100 terminals
- [ ] Tenant isolation verified — cross-tenant terminal access rejected
- [ ] Code reviewed and approved
- [ ] Documentation updated (API docs, terminal provisioning runbook)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
