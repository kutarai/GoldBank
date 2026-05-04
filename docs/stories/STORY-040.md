# STORY-040: ISO 8583 Adapter

**Epic:** EPIC-008 National Network Switching
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 5

---

## User Story

As a **system**
I want to **format and parse ISO 8583 messages**
So that **transactions can be routed to legacy national switches that use the ISO 8583 protocol**

---

## Description

### Background

Most national payment switches in Southern Africa still operate on ISO 8583, the international standard for financial transaction card-originated interchange messaging. This binary protocol has been the backbone of interbank payment routing for decades and remains the primary interface for ATM networks, POS authorization, and real-time interbank transfers across the region.

GoldBank's Switching Server is a satellite service in the Modular Monolith architecture. It must communicate with these legacy switches by constructing and parsing ISO 8583 messages with perfect fidelity. The adapter encapsulates all ISO 8583 complexity behind the `ISwitchAdapter` interface, allowing the Message Router (STORY-042) to work with a clean canonical message format while this adapter handles the binary encoding, bitmap management, data element packing, and MAC computation required by the national scheme specification.

The adapter must support the full lifecycle: building outbound authorization requests, parsing inbound responses, handling network management messages (sign-on, echo test, key exchange), and maintaining persistent TCP/IP connections with connection pooling to the switch host.

**Functional Requirements:** FR-031 (National Switch ISO 8583 Integration)

### Scope

**In scope:**
- ISO 8583 message builder supporting all required data elements for the national scheme
- ISO 8583 parser that decodes binary messages into structured data
- Bitmap management (primary and secondary bitmaps, 128 data elements)
- Data element packing/unpacking for all field types (numeric, alphanumeric, binary, LLVAR, LLLVAR)
- MAC generation for outbound messages via HSM Interface Service (STORY-021)
- MAC verification for inbound messages via HSM Interface Service
- TCP/IP socket management with connection pooling
- Asynchronous send/receive with correlation by STAN + RRN
- Full message logging (raw hex and parsed JSON) to the switching schema
- Network management messages: sign-on (0800), echo test (0800), key exchange (0800)
- MTI support: 0100/0110 (authorization), 0200/0210 (financial), 0400/0410 (reversal), 0800/0810 (network management)
- Implementation of `ISwitchAdapter` interface for adapter pattern compliance

**Out of scope:**
- ISO 20022 message handling (STORY-041)
- Message routing logic (STORY-042)
- Settlement file generation (handled by reconciliation STORY-045)
- HSM hardware provisioning (STORY-021 prerequisite)
- National switch sandbox provisioning (external dependency)
- Batch file transfers (out of scope for real-time adapter)

### User Flow

This is a system-to-system adapter. The primary interaction flows are:

**Outbound Flow (GoldBank to National Switch):**
1. Message Router calls `ISwitchAdapter.SendAsync(CanonicalMessage)` on the ISO 8583 adapter
2. Adapter maps canonical message fields to ISO 8583 data elements
3. Adapter builds the binary message: MTI + primary bitmap + secondary bitmap + packed data elements
4. Adapter requests MAC generation from HSM Interface Service for DE-64
5. Adapter appends MAC to the message
6. Adapter selects a TCP connection from the pool and sends the message with a 2-byte length header
7. Adapter registers a pending correlation entry keyed by STAN + RRN
8. Adapter awaits the response on the same connection (async with timeout)
9. Response is received, parsed, MAC verified, and returned as a `CanonicalMessage`
10. Full message (request and response) logged to `switch_messages` table

**Inbound Flow (National Switch to GoldBank):**
1. TCP listener receives a message on the listening port
2. Adapter reads the 2-byte length header, then reads the full message body
3. Adapter parses the binary message: extract MTI, decode bitmaps, unpack data elements
4. Adapter verifies the MAC via HSM Interface Service
5. Adapter converts parsed data to a `CanonicalMessage`
6. Adapter passes the canonical message to the Message Router for processing
7. After processing, the adapter formats the response as ISO 8583 and sends it back
8. Full message logged to `switch_messages` table

---

## Acceptance Criteria

- [ ] ISO 8583 message builder correctly constructs binary messages with MTI, primary bitmap, secondary bitmap, and all required data elements
- [ ] ISO 8583 parser correctly decodes binary messages into structured data element collections
- [ ] All required data elements are supported: DE-0 (MTI), DE-2 (PAN), DE-3 (processing code), DE-4 (amount), DE-11 (STAN), DE-12 (local transaction time), DE-13 (local transaction date), DE-22 (POS entry mode), DE-25 (POS condition code), DE-32 (acquiring institution ID), DE-37 (retrieval reference number), DE-38 (authorization ID response), DE-39 (response code), DE-41 (card acceptor terminal ID), DE-42 (card acceptor ID), DE-43 (card acceptor name/location), DE-48 (additional data), DE-49 (currency code), DE-52 (PIN block), DE-64 (MAC)
- [ ] MAC generation for outbound messages is performed via HSM Interface Service using the TAK key reference
- [ ] MAC verification for inbound messages is performed via HSM Interface Service and invalid MACs result in message rejection
- [ ] Messages comply with the national scheme specification (field lengths, encoding, bitmap format)
- [ ] Full message logging captures: direction, MTI, STAN, RRN, raw hex, parsed JSON, and timestamp in the `switch_messages` table
- [ ] TCP/IP connection pool maintains between 2 and 10 connections to the switch endpoint
- [ ] Async send/receive correlates responses to requests using STAN + RRN composite key
- [ ] Connection health is monitored via periodic echo test messages (0800 MTI, every 30 seconds)
- [ ] Network sign-on (0800 with processing code 001) is performed automatically on connection establishment
- [ ] Message builder handles both primary bitmap (DE 1-64) and secondary bitmap (DE 65-128) correctly
- [ ] LLVAR and LLLVAR fields are correctly length-prefixed during packing and correctly parsed during unpacking

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `GoldBank.Switching` | `src/Satellites/GoldBank.Switching/` | Satellite service project |
| `ISO8583Adapter.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO8583/` | `ISwitchAdapter` implementation for ISO 8583 |
| `ISO8583MessageBuilder.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO8583/` | Constructs binary ISO 8583 messages from data elements |
| `ISO8583MessageParser.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO8583/` | Parses binary ISO 8583 messages into data element collections |
| `DataElementDefinition.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO8583/` | Metadata for each DE (type, length, encoding) |
| `BitmapCodec.cs` | `src/Satellites/GoldBank.Switching/Adapters/ISO8583/` | Primary/secondary bitmap encode/decode |
| `FieldPackers/` | `src/Satellites/GoldBank.Switching/Adapters/ISO8583/FieldPackers/` | Packers for each field type (numeric, alpha, binary, LLVAR, LLLVAR) |
| `TcpConnectionPool.cs` | `src/Satellites/GoldBank.Switching/Network/` | Managed TCP socket pool with health checks |
| `TcpConnectionManager.cs` | `src/Satellites/GoldBank.Switching/Network/` | Connection lifecycle, reconnection, keep-alive |
| `MessageCorrelator.cs` | `src/Satellites/GoldBank.Switching/Correlation/` | Async request/response correlation by STAN+RRN |
| `SwitchMessageLogger.cs` | `src/Satellites/GoldBank.Switching/Logging/` | Persists raw and parsed messages to database |
| `ISwitchAdapter.cs` | `src/Satellites/GoldBank.Switching/Adapters/` | Interface: `SendAsync`, `ParseInbound`, `FormatOutbound` |
| `switching_service.proto` | `src/Shared/GoldBank.Protos/` | gRPC proto for internal Switching Service API |

### API / gRPC Endpoints

The ISO 8583 adapter is an internal component — it does not expose gRPC endpoints directly. It implements the `ISwitchAdapter` interface consumed by the Message Router.

**ISwitchAdapter Interface:**

```csharp
public interface ISwitchAdapter
{
    /// <summary>
    /// Sends a canonical message to the external switch and returns the response.
    /// </summary>
    Task<CanonicalMessage> SendAsync(
        CanonicalMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a raw inbound message into the canonical format.
    /// </summary>
    CanonicalMessage ParseInbound(byte[] rawMessage);

    /// <summary>
    /// Formats a canonical message into the protocol-specific wire format.
    /// </summary>
    byte[] FormatOutbound(CanonicalMessage message);

    /// <summary>
    /// Returns the protocol type this adapter handles.
    /// </summary>
    SwitchProtocol Protocol { get; }

    /// <summary>
    /// Performs connection health check (e.g., echo test).
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs sign-on to the switch.
    /// </summary>
    Task<bool> SignOnAsync(CancellationToken cancellationToken = default);
}
```

**Data Element Mapping (Canonical to ISO 8583):**

```
CanonicalMessage.MessageType       → DE-0  (MTI: 0100, 0200, 0400, 0800)
CanonicalMessage.SourceAccount     → DE-2  (PAN, LLVAR up to 19 digits)
CanonicalMessage.TransactionType   → DE-3  (Processing Code: 00=purchase, 01=cash advance, 20=refund)
CanonicalMessage.Amount            → DE-4  (Amount, 12-digit right-justified zero-filled)
CanonicalMessage.TransactionRef    → DE-11 (STAN, 6-digit numeric)
CanonicalMessage.Timestamp         → DE-12 (Time: HHmmss) + DE-13 (Date: MMDD)
CanonicalMessage.POSEntryMode      → DE-22 (POS entry mode: 051=chip, 071=contactless, 012=magnetic)
CanonicalMessage.POSCondition      → DE-25 (POS condition: 00=normal, 08=mail/telephone)
CanonicalMessage.SourceInstitution → DE-32 (Acquiring institution, LLVAR up to 11 digits)
CanonicalMessage.RetrievalRef      → DE-37 (Retrieval reference number, 12 alphanumeric)
CanonicalMessage.AuthCode          → DE-38 (Authorization ID response, 6 alphanumeric)
CanonicalMessage.ResponseCode      → DE-39 (Response code, 2 alphanumeric: 00=approved)
CanonicalMessage.TerminalId        → DE-41 (Terminal ID, 8 alphanumeric)
CanonicalMessage.MerchantId        → DE-42 (Merchant ID, 15 alphanumeric)
CanonicalMessage.MerchantName      → DE-43 (Merchant name/location, 40 alphanumeric)
CanonicalMessage.AdditionalData    → DE-48 (Additional data, LLLVAR)
CanonicalMessage.Currency          → DE-49 (Currency code: 710=ZAR, 072=BWP, 480=MUR)
CanonicalMessage.PINBlock          → DE-52 (PIN block, 8 bytes binary)
CanonicalMessage.MAC               → DE-64 (MAC, 8 bytes binary — computed via HSM)
```

### Database Changes

**switch_messages table** (in the `switching` schema):

```sql
CREATE TABLE switching.switch_messages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(50) NOT NULL,
    direction       VARCHAR(10) NOT NULL CHECK (direction IN ('inbound', 'outbound')),
    protocol        VARCHAR(20) NOT NULL DEFAULT 'ISO8583',
    mti             VARCHAR(4) NOT NULL,
    stan            VARCHAR(6),
    rrn             VARCHAR(12),
    processing_code VARCHAR(6),
    response_code   VARCHAR(2),
    amount          DECIMAL(18, 2),
    currency        VARCHAR(3),
    raw_hex         TEXT NOT NULL,
    parsed_json     JSONB NOT NULL,
    source_institution   VARCHAR(20),
    dest_institution     VARCHAR(20),
    terminal_id     VARCHAR(8),
    merchant_id     VARCHAR(15),
    mac_valid       BOOLEAN,
    correlation_id  UUID,
    processing_time_ms INT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_switch_messages_tenant_time ON switching.switch_messages (tenant_id, created_at DESC);
CREATE INDEX idx_switch_messages_stan_rrn ON switching.switch_messages (stan, rrn);
CREATE INDEX idx_switch_messages_mti ON switching.switch_messages (mti, created_at DESC);
CREATE INDEX idx_switch_messages_correlation ON switching.switch_messages (correlation_id);
CREATE INDEX idx_switch_messages_direction_time ON switching.switch_messages (direction, created_at DESC);
```

**switch_connections table** (tracks connection pool state):

```sql
CREATE TABLE switching.switch_connections (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    switch_endpoint VARCHAR(100) NOT NULL,
    connection_index INT NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'disconnected',
    last_activity   TIMESTAMPTZ,
    messages_sent   BIGINT DEFAULT 0,
    messages_received BIGINT DEFAULT 0,
    last_sign_on    TIMESTAMPTZ,
    last_echo_test  TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (switch_endpoint, connection_index)
);
```

### Security Considerations

- **MAC Generation (DE-64):** Every outbound ISO 8583 message must include a Message Authentication Code in DE-64, generated by the HSM Interface Service using the Terminal Authentication Key (TAK). The MAC is computed over all data elements preceding DE-64 using ISO 9797-1 Algorithm 3 (Retail MAC with TDES). The HSM `GenerateMAC` RPC is called with the packed message bytes (excluding DE-64) and the TAK reference.
- **MAC Verification:** Every inbound message must have its MAC verified via the HSM `VerifyMAC` RPC before any processing occurs. Messages with invalid MACs are rejected with response code "96" (system malfunction) and flagged in the audit log.
- **PIN Block (DE-52):** PIN blocks are encrypted using ISO 9564 Format 0 within the HSM boundary. The adapter never handles cleartext PINs — it receives pre-encrypted PIN blocks from the Core Banking module and passes them through. For inbound messages, encrypted PIN blocks are forwarded to Core Banking for HSM decryption.
- **PAN Masking in Logs:** When logging parsed messages to `switch_messages.parsed_json`, the PAN (DE-2) must be masked: show first 6 and last 4 digits only (e.g., `411111******1234`). Raw hex is logged in full but access to the `switch_messages` table must be restricted to operations staff.
- **TLS for TCP Connections:** If the national switch supports TLS-wrapped TCP, configure `SslStream` over the TCP socket. Certificate pinning should be used for the switch's server certificate.
- **Connection Authentication:** Sign-on messages (MTI 0800, processing code 001) authenticate the GoldBank node to the switch. Sign-on credentials (institution code, terminal ID) are stored in encrypted configuration, not in source code.

### Edge Cases

- **Connection Loss Mid-Message:** If the TCP connection drops while awaiting a response, the correlator must time out the pending request after the configured timeout (30 seconds), return a decline code "96" to the caller, and initiate a reversal (MTI 0400) for financial messages that may have been received by the switch.
- **Partial Message Receipt:** TCP is a stream protocol — the adapter must handle partial reads. Use the 2-byte length header to know exactly how many bytes to expect, and buffer until the full message is received.
- **STAN Wraparound:** STAN is a 6-digit number (000001-999999). When it wraps around, ensure no active correlation entry uses the recycled STAN. The correlator uses STAN + RRN composite key to minimize collision risk.
- **Duplicate STAN+RRN:** If a response arrives for an already-completed correlation entry (e.g., late response after timeout), log a warning and discard the duplicate.
- **Switch Timeout (No Response):** If no response is received within 30 seconds (configurable), return response code "96" (system malfunction) and log the timeout. For MTI 0200 (financial), the system must initiate an automatic reversal (MTI 0400).
- **Invalid Message Format:** If a received message cannot be parsed (corrupt bitmap, invalid field lengths), log the raw hex, reject the message, and do not propagate to the router.
- **HSM Unavailable:** If the HSM is unreachable when MAC generation is needed, the message cannot be sent. Return an error to the caller and log the HSM failure. Do not send messages without a MAC.
- **Connection Pool Exhaustion:** If all TCP connections are in use, queue the request with a bounded wait (5 seconds). If still no connection available, return decline code "96".
- **Echo Test Failure:** If 3 consecutive echo test messages fail on a connection, mark it as unhealthy and attempt reconnection. Remove from the active pool during reconnection.
- **Bitmap Overflow:** If a data element beyond DE-64 is needed, the secondary bitmap flag (bit 1 of primary bitmap) must be set. Validate this during message construction.

---

## Dependencies

**Prerequisite Stories:**
- STORY-021: HSM Interface Service — required for MAC generation (DE-64) and MAC verification via `GenerateMAC` / `VerifyMAC` RPCs
- STORY-004: gRPC Proto Definitions — proto files for Switching Service internal API

**Blocked Stories:**
- STORY-042: Message Router & Canonical Format — the router depends on this adapter being available
- STORY-043: Outbound Transaction Routing — outbound routing uses this adapter for ISO 8583 destinations
- STORY-044: Inbound Transaction Processing — inbound processing uses this adapter to parse ISO 8583 messages
- STORY-045: Daily Reconciliation — reconciliation reads from `switch_messages` table populated by this adapter

**External Dependencies:**
- National switch sandbox environment for integration testing (TCP/IP endpoint, credentials, test cards)
- National scheme specification document (data element definitions, field lengths, encoding rules)
- Switch-specific MTI and processing code mappings
- Test certificates for TLS-wrapped TCP connections (if applicable)
- SoftHSM2 for development/CI (HSM dependency via STORY-021)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — message builder/parser tested against known ISO 8583 test vectors
- [ ] Integration tests passing with mock TCP switch server and SoftHSM2
- [ ] All required data elements (DE-0 through DE-64) correctly packed and unpacked
- [ ] MAC generation and verification tested with HSM Interface Service
- [ ] TCP connection pool tested under concurrent load (min 10 simultaneous requests)
- [ ] Message logging verified — all messages captured in `switch_messages` with correct raw hex and parsed JSON
- [ ] PAN masking verified in log output
- [ ] Code reviewed and approved
- [ ] Documentation updated (adapter usage, data element mapping, configuration)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
