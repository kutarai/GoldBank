# STORY-044: Inbound Transaction Processing

**Epic:** EPIC-008 National Network Switching
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 5

---

## User Story

As a **system**
I want to **process incoming transactions from the national switch**
So that **users can receive payments from external institutions**

---

## Description

### Background

UniBank customers do not only send money — they also receive it. When a customer at another bank initiates a transfer to a UniBank account, or when a POS transaction is processed where UniBank is the issuing bank, the national switch delivers an inbound message to UniBank's Switching Server. This message must be received, parsed, validated, and routed to Core Banking for account crediting.

Inbound processing is the mirror image of outbound routing. The Switching Server listens for incoming messages on TCP ports (for ISO 8583) and webhook endpoints (for ISO 20022). When a message arrives, the appropriate adapter parses it into the canonical format, and the inbound handler validates the transaction before forwarding it to Core Banking. After Core Banking processes the credit (or rejects it), the handler formats the response and sends it back to the switch via the adapter.

Response time is critical for inbound processing. National switches typically expect a response within 15-30 seconds. If UniBank does not respond in time, the switch may time out and the sending institution's customer sees a failed transaction. The inbound handler must be fast and resilient.

**Functional Requirements:** FR-029 (Inbound Transaction Processing)

### Scope

**In scope:**
- TCP listener on a configured port for incoming ISO 8583 messages
- HTTP webhook endpoint for incoming ISO 20022 messages
- Adapter selection based on message format/source
- Parsing inbound messages to canonical format via the appropriate adapter
- Validation: destination account exists, account is active, amount is valid, currency is supported
- Publishing `ProcessInboundTransaction` Wolverine command to Core Banking
- Receiving Core Banking's processing result
- Building and sending the response (approve/decline) back to the switch
- Standard decline codes: "14" (invalid card/account), "51" (insufficient funds for refund/reversal), "78" (account not found), "96" (system error)
- Full message logging for all inbound messages (request and response)
- Handling network management messages (echo test, sign-on) at the adapter level

**Out of scope:**
- Adapter implementation details (STORY-040, STORY-041)
- Message Router implementation (STORY-042)
- Outbound transaction routing (STORY-043)
- Fraud detection on inbound transactions (future enhancement)
- Real-time notification to the customer upon credit (handled by Core Banking/Notifications)

### User Flow

**Inbound Credit Transfer Flow (e.g., incoming EFT):**

1. **External Event:** A customer at Bank X initiates a transfer to a UniBank account
2. **Switch Delivery:** The national switch routes the message to UniBank's Switching Server
3. **TCP/HTTP Receipt:** The Switching Server receives the raw message on the TCP listener (ISO 8583) or webhook endpoint (ISO 20022)
4. **Adapter Detection:** The inbound listener determines the adapter based on the source (TCP = ISO 8583, HTTP = ISO 20022)
5. **Message Parsing:** The adapter parses the raw message into a `CanonicalMessage`
6. **MAC Verification (ISO 8583):** For ISO 8583 messages, the adapter verifies the MAC via HSM Interface Service. Invalid MAC = reject with "96"
7. **Inbound Validation:**
   - Destination account (DE-2 / CdtrAcct) exists in UniBank
   - Account status is active (not frozen, closed, or dormant)
   - Amount is valid (positive, reasonable range)
   - Currency is supported by the destination account
8. **Publish to Core Banking:** Handler publishes `ProcessInboundTransaction` Wolverine command
9. **Core Banking Credits:** Core Banking credits the destination account, creates a transaction record
10. **Result Received:** Core Banking returns success or failure
11. **Build Response:** Handler builds the response canonical message:
    - Approved: response code "00", authorization code generated
    - Declined: appropriate decline code (see decline code table)
12. **Format and Send:** Adapter formats the response into protocol-specific format and sends it back to the switch
13. **Log Everything:** Both inbound request and outbound response are logged to `switch_messages`
14. **Customer Notification:** Core Banking triggers a push notification / SMS to the UniBank customer (handled separately)

**Inbound POS Authorization Flow:**

1. Switch delivers an authorization request (MTI 0100) for a UniBank cardholder
2. Parsing and validation same as above
3. Additional checks: card not blocked, PIN verification (if PIN present), available balance check
4. Core Banking performs authorization hold on the cardholder's account
5. Response sent: "00" (approved) or "51" (insufficient funds) or "14" (invalid card)

---

## Acceptance Criteria

- [ ] TCP listener receives incoming ISO 8583 messages on the configured port and passes them to the ISO 8583 adapter for parsing
- [ ] HTTP webhook endpoint receives incoming ISO 20022 messages and passes them to the ISO 20022 adapter for parsing
- [ ] Inbound messages are correctly parsed to canonical format by the appropriate adapter
- [ ] Validation checks are performed: destination account exists, account active, amount valid, currency supported
- [ ] `ProcessInboundTransaction` Wolverine command is published to Core Banking with all required fields
- [ ] Core Banking credits the destination account upon receiving the command (or rejects with a reason)
- [ ] Response is built with the correct response code and sent back to the switch within the expected time window
- [ ] Invalid destination account returns decline code "14" (invalid card/account)
- [ ] Insufficient funds for refund/reversal returns decline code "51"
- [ ] Account not found returns decline code "78"
- [ ] System errors return decline code "96"
- [ ] All inbound messages (request and response) are logged to `switch_messages` table
- [ ] MAC verification is performed for ISO 8583 inbound messages; invalid MAC results in rejection with code "96"
- [ ] Network management messages (echo test 0800, sign-on 0800) are handled at the adapter level without routing to Core Banking

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `InboundTcpListener.cs` | `src/Satellites/UniBank.Switching/Listeners/` | TCP socket listener for ISO 8583 inbound messages |
| `InboundWebhookController.cs` | `src/Satellites/UniBank.Switching/Controllers/` | HTTP endpoint for ISO 20022 inbound messages |
| `InboundMessageHandler.cs` | `src/Satellites/UniBank.Switching/Handlers/` | Orchestrates inbound validation and routing to Core Banking |
| `InboundValidator.cs` | `src/Satellites/UniBank.Switching/Validation/` | Validates inbound transaction fields |
| `AccountLookupService.cs` | `src/Satellites/UniBank.Switching/Services/` | Looks up destination account in Core Banking via gRPC |
| `ProcessInboundTransactionHandler.cs` | `src/Core/UniBank.Core/Modules/Payments/Handlers/` | Core Banking handler that credits the account |
| `DeclineCodeMapper.cs` | `src/Satellites/UniBank.Switching/Mapping/` | Maps validation failures to ISO 8583/20022 decline codes |
| `SwitchMessageLogger.cs` | `src/Satellites/UniBank.Switching/Logging/` | Shared message logger |

### API / gRPC Endpoints

**Inbound Webhook Endpoint (ISO 20022):**

```
POST /api/v1/switch/inbound
Content-Type: application/xml  (or application/json for JSON-mode switches)
X-Switch-Id: {switch-identifier}

Body: ISO 20022 XML document (pacs.008 for credit transfer)

Response: ISO 20022 XML document (pacs.002 status report)
HTTP 200 with response body (both approve and decline)
HTTP 400 for malformed requests
HTTP 500 for system errors
```

**Account Lookup gRPC (Switching Server calls Core Banking):**

```protobuf
service AccountLookupService {
  rpc LookupAccount (LookupAccountRequest) returns (LookupAccountResponse);
}

message LookupAccountRequest {
  string account_identifier = 1;  // PAN or account number
  string tenant_id = 2;
}

message LookupAccountResponse {
  bool found = 1;
  string account_id = 2;
  string account_status = 3;      // active, frozen, closed, dormant
  string currency = 4;
  string account_holder_name = 5;
  bool accepts_credits = 6;
}
```

**InboundTcpListener (pseudocode):**

```csharp
public class InboundTcpListener : BackgroundService
{
    private readonly int _port;
    private readonly ISO8583Adapter _adapter;
    private readonly InboundMessageHandler _handler;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            _ = HandleConnectionAsync(client, stoppingToken);
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();
        while (client.Connected && !ct.IsCancellationRequested)
        {
            // Read 2-byte length header
            var lengthBytes = new byte[2];
            await stream.ReadExactlyAsync(lengthBytes, ct);
            var messageLength = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);

            // Read full message
            var messageBytes = new byte[messageLength];
            await stream.ReadExactlyAsync(messageBytes, ct);

            // Parse and handle
            var canonical = _adapter.ParseInbound(messageBytes);

            // Check if network management (handle locally) or transaction (route to Core)
            if (canonical.MessageType == MessageType.NetworkManagement)
            {
                var response = HandleNetworkManagement(canonical);
                var responseBytes = _adapter.FormatOutbound(response);
                await SendWithLengthHeader(stream, responseBytes, ct);
            }
            else
            {
                var result = await _handler.HandleAsync(canonical, ct);
                var responseBytes = _adapter.FormatOutbound(result);
                await SendWithLengthHeader(stream, responseBytes, ct);
            }
        }
    }
}
```

**Decline Code Mapping Table:**

| Validation Failure | ISO 8583 Code | ISO 20022 Status | Description |
|---|---|---|---|
| Account not found | 14 | RJCT/AC04 | Invalid card/account number |
| Account closed | 14 | RJCT/AC04 | Account closed |
| Account frozen | 78 | RJCT/AC06 | Account blocked |
| Account dormant | 78 | RJCT/AC06 | Account blocked |
| Insufficient funds (reversal) | 51 | RJCT/AM04 | Insufficient funds |
| Invalid amount | 13 | RJCT/AM09 | Invalid amount |
| Currency mismatch | 12 | RJCT/AM03 | Currency not supported |
| Invalid MAC | 96 | RJCT/FF01 | System malfunction |
| System error | 96 | RJCT/FF01 | System malfunction |
| Timeout processing | 96 | RJCT/FF01 | System malfunction |

### Database Changes

**inbound_transactions table** (tracks inbound transaction lifecycle):

```sql
CREATE TABLE switching.inbound_transactions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           VARCHAR(50) NOT NULL,
    transaction_ref     VARCHAR(50) NOT NULL,
    retrieval_ref       VARCHAR(12),
    source_institution  VARCHAR(20) NOT NULL,
    dest_institution    VARCHAR(20) NOT NULL,
    dest_account        VARCHAR(34) NOT NULL,
    adapter_type        VARCHAR(20) NOT NULL,
    amount              DECIMAL(18, 2) NOT NULL,
    currency            VARCHAR(3) NOT NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'received',
    validation_result   VARCHAR(20),
    response_code       VARCHAR(4),
    processing_time_ms  INT,
    error_message       VARCHAR(500),
    switch_message_id   UUID REFERENCES switching.switch_messages(id),
    core_transaction_id UUID,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_inbound_txn_tenant_time ON switching.inbound_transactions (tenant_id, created_at DESC);
CREATE INDEX idx_inbound_txn_ref ON switching.inbound_transactions (transaction_ref);
CREATE INDEX idx_inbound_txn_status ON switching.inbound_transactions (status);
CREATE INDEX idx_inbound_txn_dest_account ON switching.inbound_transactions (dest_account, created_at DESC);
```

### Security Considerations

- **TCP Listener Security:** The TCP listener port should only be accessible from the national switch IP address(es). Use firewall rules (iptables/security groups) to restrict inbound TCP connections to known switch IPs.
- **Webhook Authentication:** The ISO 20022 webhook endpoint must authenticate inbound requests. Options:
  - mTLS: require the switch to present a valid client certificate
  - API key: require a pre-shared API key in a custom header
  - IP whitelisting: restrict webhook access to known switch IPs
- **MAC Verification First:** For ISO 8583 messages, MAC verification must be the FIRST validation step. If the MAC is invalid, the message is rejected immediately — no further processing occurs. This prevents processing tampered messages.
- **Account Enumeration Prevention:** Decline codes should not reveal whether an account exists but is frozen vs. does not exist at all. Use a generic "unable to process" response if the switch specification allows it. However, most national schemes require specific decline codes — follow the scheme specification.
- **Rate Limiting:** Implement rate limiting on the inbound listener to prevent denial-of-service. Max 1000 messages per second per source IP (configurable).
- **Input Sanitization:** All parsed fields must be sanitized before database storage and before passing to Core Banking. Prevent SQL injection via parameterized queries (EF Core handles this). Prevent log injection by sanitizing log output.

### Edge Cases

- **Unknown Account Format:** If the destination account identifier does not match any known format (PAN, IBAN, local account number), return decline code "14" and log the raw identifier for investigation.
- **Duplicate Inbound Transaction:** If the same transaction reference arrives twice (switch retry), check `inbound_transactions` for an existing entry. If the first is still processing, wait briefly (2 seconds). If already completed, return the same response as the first time (idempotent).
- **Core Banking Timeout:** If Core Banking does not respond to the `ProcessInboundTransaction` command within 15 seconds, return decline code "96" to the switch. The transaction can be retried by the switch.
- **Core Banking Rejection:** If Core Banking rejects the credit (e.g., account has a credit block, regulatory hold), return the appropriate decline code.
- **Partial TCP Read:** Handle partial TCP reads by buffering. Use `ReadExactlyAsync` (or equivalent) to ensure the full message body is read based on the length header.
- **Connection Flood:** If many TCP connections arrive simultaneously (switch reconnection storm), use a `SemaphoreSlim` to limit concurrent connection handling (max 50 concurrent connections).
- **Network Management Messages:** Echo test (0800) and sign-on (0800) messages must be handled by the adapter/listener directly without routing to Core Banking. Respond immediately with the appropriate response MTI (0810).
- **Malformed Messages:** If a message cannot be parsed at all (e.g., corrupted data, wrong protocol on the wrong port), log the raw bytes at ERROR level and close the connection gracefully.
- **Multi-Tenant Routing:** Inbound messages do not contain a UniBank tenant ID. The tenant must be resolved from the destination account. The `AccountLookupService` returns the tenant ID associated with the account, which is then propagated through the processing chain.

---

## Dependencies

**Prerequisite Stories:**
- STORY-042: Message Router & Canonical Format — canonical message definition and routing infrastructure
- STORY-040: ISO 8583 Adapter — parses inbound ISO 8583 messages (indirectly via STORY-042)
- STORY-041: ISO 20022 Adapter — parses inbound ISO 20022 messages (indirectly via STORY-042)

**Blocked Stories:**
- STORY-045: Daily Reconciliation — reconciliation matches inbound transactions against switch records

**External Dependencies:**
- National switch sandbox for inbound message testing (switch must be able to send test messages to UniBank)
- Firewall rules to allow inbound TCP connections from switch IPs
- Core Banking account lookup service must be available

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — inbound handler, validator, and decline code mapper tested
- [ ] Integration tests passing with mock TCP client and mock HTTP client sending test messages
- [ ] All decline codes verified for each validation failure scenario
- [ ] MAC verification tested for ISO 8583 messages (valid and invalid MACs)
- [ ] Idempotency tested for duplicate inbound messages
- [ ] Network management messages (echo test, sign-on) handled correctly
- [ ] Core Banking timeout handling tested
- [ ] Message logging verified for all inbound messages
- [ ] Code reviewed and approved
- [ ] Documentation updated (inbound flow diagram, decline codes, listener configuration)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
