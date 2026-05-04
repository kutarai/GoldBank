# STORY-043: Outbound Transaction Routing

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
I want **outgoing transactions routed to the national switch**
So that **users can transact with external banks and financial institutions**

---

## Description

### Background

When a GoldBank customer initiates a transaction that targets an account at another bank — whether it is a funds transfer, a POS purchase at a merchant whose acquirer is a different bank, or a bill payment routed through the national clearing system — the transaction must leave GoldBank's domain and travel through the national payment switch to reach the destination institution.

This story implements the full outbound transaction lifecycle within the Switching Server: receiving the transaction request from Core Banking via Wolverine messaging, routing it through the Message Router to select the correct adapter, formatting and sending the message to the national switch, awaiting and parsing the response, and communicating the result back to Core Banking.

The flow must be resilient. National switches can be slow, unresponsive, or return unexpected errors. The implementation uses Polly circuit breakers to prevent cascading failures, configurable timeouts with automatic decline codes, and connection pooling for TCP-based switches. For financial messages (MTI 0200) that time out, automatic reversals must be initiated to prevent one-sided transactions.

**Functional Requirements:** FR-028 (Outbound Transaction Routing)

### Scope

**In scope:**
- Wolverine command handler for `RouteOutboundTransaction`
- End-to-end outbound flow: receive command, route, format, send, receive response, publish result
- Polly circuit breaker configuration for switch connectivity
- Configurable timeout with decline code "96" on timeout
- Automatic reversal (MTI 0400) for timed-out financial messages
- Connection pool management for TCP-based switches (min 2, max 10 connections)
- `OutboundTransactionResult` Wolverine event publication back to Core Banking
- Comprehensive logging and tracing for the full outbound lifecycle
- Metrics collection: success rate, latency, timeout rate, circuit breaker state

**Out of scope:**
- Adapter implementation details (STORY-040, STORY-041)
- Message Router implementation (STORY-042)
- Inbound transaction processing (STORY-044)
- Reconciliation (STORY-045)
- Transaction initiation logic in Core Banking

### User Flow

**End-to-End Outbound Transaction Flow:**

1. **Customer Action:** A GoldBank customer initiates an external transfer (e.g., send ZAR 500 to a Standard Bank account)
2. **Core Banking Validation:** Core Banking validates the transaction: sufficient funds, account active, fraud checks passed, daily limits not exceeded
3. **Funds Hold:** Core Banking places a hold on the source account for the transaction amount
4. **Wolverine Command:** Core Banking publishes `RouteOutboundTransaction` command to the Switching Server via Wolverine
5. **Handler Receives:** `RouteOutboundTransactionHandler` in the Switching Server receives the command
6. **Canonical Message:** Handler constructs a `CanonicalMessage` from the command data
7. **Route Resolution:** Handler calls `MessageRouter.ResolveAdapter(destinationInstitution)` to get the adapter and endpoint
8. **Format Message:** Adapter formats the canonical message into protocol-specific format (ISO 8583 binary or ISO 20022 XML)
9. **Send to Switch:** Adapter sends the message via TCP socket (ISO 8583) or REST API/MQ (ISO 20022)
10. **Await Response:** Adapter waits for the response with a configurable timeout (default 30 seconds)
11. **Parse Response:** Adapter parses the response back into canonical format
12. **Publish Result:** Handler publishes `OutboundTransactionResult` event with the response code
13. **Core Banking Completes:** Core Banking handler receives the result:
    - **Approved (response code "00"):** Complete the debit, release the hold, update transaction status to "completed"
    - **Declined (any other code):** Release the hold, update transaction status to "declined" with reason
14. **Notification:** Core Banking triggers a notification to the customer with the transaction result

**Timeout/Failure Flow:**

1. Steps 1-9 same as above
2. **No Response Within Timeout:** After 30 seconds, the adapter returns a timeout indication
3. **Decline with "96":** Handler publishes `OutboundTransactionResult` with response code "96" (system malfunction)
4. **Automatic Reversal:** For financial messages (MTI 0200), handler initiates a reversal:
   a. Constructs a reversal canonical message (MessageType = Reversal)
   b. Routes through the same adapter with the original transaction reference
   c. Sends reversal (MTI 0400) to the switch
   d. Logs reversal attempt and result
5. **Core Banking Handles Decline:** Core Banking releases the funds hold, marks transaction as "failed"
6. **Customer Notification:** Customer notified of transaction failure

---

## Acceptance Criteria

- [ ] Core Banking can publish a `RouteOutboundTransaction` Wolverine command and the Switching Server handler receives and processes it
- [ ] Handler correctly constructs a `CanonicalMessage` and routes it through the `MessageRouter`
- [ ] Message is formatted via the selected adapter (ISO 8583 or ISO 20022) and sent to the switch
- [ ] Response from the switch is received, parsed to canonical format, and an `OutboundTransactionResult` event is published
- [ ] Core Banking receives the `OutboundTransactionResult` and completes or fails the transaction accordingly
- [ ] Timeout handling: if no response within 30 seconds (configurable), response code "96" (system malfunction) is returned
- [ ] For financial messages (MTI 0200) that timeout, an automatic reversal (MTI 0400) is initiated
- [ ] Polly circuit breaker opens after 5 failures within 60 seconds, remains open for 30 seconds, then transitions to half-open
- [ ] During circuit breaker open state, outbound requests immediately return decline code "96" without attempting to send
- [ ] TCP connection pool maintains minimum 2 and maximum 10 connections per switch endpoint
- [ ] Connection pool handles connection exhaustion gracefully with bounded wait (5 seconds) before declining
- [ ] Full transaction lifecycle is logged: command received, route resolved, message sent, response received/timeout, result published
- [ ] Metrics are emitted: transaction count, success/decline/timeout rates, latency percentiles, circuit breaker state changes

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `RouteOutboundTransactionHandler.cs` | `src/Satellites/GoldBank.Switching/Handlers/` | Wolverine handler for outbound routing |
| `OutboundTransactionOrchestrator.cs` | `src/Satellites/GoldBank.Switching/Orchestration/` | Orchestrates the send/receive/reversal lifecycle |
| `ReversalService.cs` | `src/Satellites/GoldBank.Switching/Services/` | Handles automatic reversal for timed-out financial messages |
| `CircuitBreakerConfig.cs` | `src/Satellites/GoldBank.Switching/Resilience/` | Polly circuit breaker configuration per switch endpoint |
| `SwitchMetrics.cs` | `src/Satellites/GoldBank.Switching/Telemetry/` | Prometheus metrics for switch connectivity |
| `TcpConnectionPool.cs` | `src/Satellites/GoldBank.Switching/Network/` | TCP socket pool (shared with STORY-040) |
| `MessageRouter.cs` | `src/Satellites/GoldBank.Switching/Routing/` | Route resolution (from STORY-042) |
| `ISwitchAdapter.cs` | `src/Satellites/GoldBank.Switching/Adapters/` | Adapter interface (from STORY-040/041) |

### API / gRPC Endpoints

No external API endpoints. This story is entirely internal messaging via Wolverine.

**Wolverine Command/Event Flow:**

```
Core Banking                          Switching Server
    |                                       |
    |-- RouteOutboundTransaction -->        |
    |                                       |-- [resolve adapter] -->
    |                                       |-- [format & send] -->  National Switch
    |                                       |<-- [receive & parse] --
    |                                       |
    |<-- OutboundTransactionResult --       |
    |                                       |
```

**RouteOutboundTransactionHandler:**

```csharp
public class RouteOutboundTransactionHandler
{
    private readonly IMessageRouter _router;
    private readonly OutboundTransactionOrchestrator _orchestrator;
    private readonly ILogger<RouteOutboundTransactionHandler> _logger;

    public async Task<OutboundTransactionResult> Handle(
        RouteOutboundTransaction command,
        CancellationToken cancellationToken)
    {
        // 1. Build canonical message from command
        var canonical = MapToCanonical(command);

        // 2. Resolve adapter via router
        var (adapter, endpointConfig) = await _router.ResolveAdapter(
            command.DestinationInstitution, cancellationToken);

        // 3. Orchestrate send/receive with circuit breaker and timeout
        var result = await _orchestrator.ExecuteAsync(
            canonical, adapter, endpointConfig, cancellationToken);

        // 4. Return result (Wolverine cascading message)
        return result;
    }
}
```

**OutboundTransactionOrchestrator (pseudocode):**

```csharp
public async Task<OutboundTransactionResult> ExecuteAsync(
    CanonicalMessage message,
    ISwitchAdapter adapter,
    EndpointConfig config,
    CancellationToken cancellationToken)
{
    // Circuit breaker wraps the entire operation
    return await _circuitBreakerPolicy.ExecuteAsync(async (ct) =>
    {
        // Timeout policy
        using var timeoutCts = CancellationTokenSource
            .CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

        try
        {
            var response = await adapter.SendAsync(message, timeoutCts.Token);
            return BuildResult(message, response, approved: response.ResponseCode == "00");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout — initiate reversal for financial messages
            if (message.MessageType == MessageType.Financial)
            {
                await _reversalService.InitiateReversalAsync(message, adapter, ct);
            }
            return BuildResult(message, responseCode: "96", approved: false,
                declineReason: "Switch timeout");
        }
    }, cancellationToken);
}
```

**Polly Circuit Breaker Configuration:**

```csharp
var circuitBreakerPolicy = Policy
    .Handle<Exception>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (exception, duration) =>
        {
            _logger.LogWarning("Circuit breaker OPEN for {Duration}s: {Error}",
                duration.TotalSeconds, exception.Message);
            _metrics.CircuitBreakerStateChanged("open");
        },
        onReset: () =>
        {
            _logger.LogInformation("Circuit breaker RESET");
            _metrics.CircuitBreakerStateChanged("closed");
        },
        onHalfOpen: () =>
        {
            _logger.LogInformation("Circuit breaker HALF-OPEN");
            _metrics.CircuitBreakerStateChanged("half-open");
        }
    );
```

**TCP Connection Pool Configuration:**

```json
{
  "Switching": {
    "ConnectionPool": {
      "MinConnections": 2,
      "MaxConnections": 10,
      "ConnectionTimeout": 5000,
      "IdleTimeout": 300000,
      "HealthCheckInterval": 30000
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "FailureWindow": 60,
      "BreakDuration": 30
    },
    "DefaultTimeout": 30
  }
}
```

### Database Changes

**outbound_transactions table** (tracks outbound transaction lifecycle):

```sql
CREATE TABLE switching.outbound_transactions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           VARCHAR(50) NOT NULL,
    transaction_ref     VARCHAR(50) NOT NULL,
    retrieval_ref       VARCHAR(12),
    source_institution  VARCHAR(20) NOT NULL,
    dest_institution    VARCHAR(20) NOT NULL,
    adapter_type        VARCHAR(20) NOT NULL,
    amount              DECIMAL(18, 2) NOT NULL,
    currency            VARCHAR(3) NOT NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    response_code       VARCHAR(2),
    authorization_code  VARCHAR(6),
    sent_at             TIMESTAMPTZ,
    response_at         TIMESTAMPTZ,
    latency_ms          INT,
    reversal_required   BOOLEAN NOT NULL DEFAULT false,
    reversal_sent       BOOLEAN NOT NULL DEFAULT false,
    reversal_response   VARCHAR(2),
    error_message       VARCHAR(500),
    switch_message_id   UUID REFERENCES switching.switch_messages(id),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_outbound_txn_tenant_time ON switching.outbound_transactions (tenant_id, created_at DESC);
CREATE INDEX idx_outbound_txn_ref ON switching.outbound_transactions (transaction_ref);
CREATE INDEX idx_outbound_txn_status ON switching.outbound_transactions (status);
CREATE INDEX idx_outbound_txn_reversal ON switching.outbound_transactions (reversal_required, reversal_sent)
    WHERE reversal_required = true;
```

### Security Considerations

- **Funds Hold Before Send:** Core Banking must place a funds hold on the source account BEFORE publishing the `RouteOutboundTransaction` command. This ensures that even if the switch approves the transaction but the result message is lost, the funds are already reserved.
- **Idempotency:** The `RouteOutboundTransaction` handler must be idempotent. If the same command is delivered twice (Wolverine at-least-once delivery), the handler should detect the duplicate by checking `outbound_transactions.transaction_ref` and return the existing result.
- **Reversal Security:** Automatic reversals must use the same STAN and RRN as the original transaction (per ISO 8583 spec) and must be sent through the same adapter and endpoint. Reversals must be retried if they fail (max 3 attempts with exponential backoff).
- **No Sensitive Data in Wolverine Messages:** The `RouteOutboundTransaction` command should not contain full PAN or cleartext PIN. PAN should be tokenized or masked. PIN blocks are pre-encrypted by the HSM.
- **Audit Trail:** Every outbound transaction attempt is recorded in `outbound_transactions` with full lifecycle tracking. This provides a complete audit trail independent of the switch message log.

### Edge Cases

- **Circuit Breaker Open:** When the circuit breaker is open, all outbound requests are immediately declined with code "96" without attempting to contact the switch. The handler must still publish `OutboundTransactionResult` so Core Banking can release the funds hold.
- **Reversal Timeout:** If the automatic reversal itself times out, log at CRITICAL level and flag the transaction for manual intervention. The `reversal_required = true, reversal_sent = false` combination indicates a pending reversal that needs attention.
- **Reversal Declined:** If the switch responds to the reversal with a decline, log the response and flag for manual reconciliation. The original transaction may have never been processed by the switch.
- **Double Spend Prevention:** If the same `TransactionReference` arrives twice (duplicate Wolverine delivery), the handler checks `outbound_transactions` for an existing entry. If found with status "completed" or "declined", return the existing result. If found with status "pending" (still in-flight), wait for the in-flight request to complete.
- **Switch Response After Timeout:** If a switch response arrives after the timeout has fired and the reversal has been sent, the late response is logged but ignored. The reversal takes precedence.
- **Connection Pool Exhaustion During Peak:** If the TCP connection pool is exhausted and the 5-second wait expires, decline with code "96" and emit a metric/alert. Consider auto-scaling the pool max if this happens frequently.
- **Partial Response:** If the TCP connection drops mid-response (partial read), treat as a timeout and initiate reversal for financial messages.
- **Mixed Protocol Destination:** If a destination institution has both ISO 8583 and ISO 20022 routes configured (future), the router uses the route with the highest priority. This story does not implement multi-route failover.

---

## Dependencies

**Prerequisite Stories:**
- STORY-042: Message Router & Canonical Format — the router that selects adapters
- STORY-040: ISO 8583 Adapter — adapter for legacy switch connectivity (indirectly via STORY-042)
- STORY-041: ISO 20022 Adapter — adapter for modern switch connectivity (indirectly via STORY-042)

**Blocked Stories:**
- STORY-045: Daily Reconciliation — reconciliation matches outbound transactions against switch records
- STORY-052: Merchant Settlement & Payout — settlement payouts to external banks use outbound routing

**External Dependencies:**
- National switch sandbox for end-to-end testing
- Wolverine messaging infrastructure (STORY-007)
- Redis for circuit breaker state sharing across instances (if multiple Switching Server instances)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — handler, orchestrator, and reversal service tested with mock adapters
- [ ] Integration tests passing with mock switch (TCP and REST) verifying full send/receive lifecycle
- [ ] Circuit breaker tested: opens after 5 failures, declines during open state, recovers in half-open
- [ ] Timeout handling tested: 30-second timeout, automatic reversal for financial messages
- [ ] Idempotency tested: duplicate commands return existing result
- [ ] Connection pool tested under concurrent load
- [ ] Metrics verified: success rate, latency, timeout rate, circuit breaker transitions
- [ ] Code reviewed and approved
- [ ] Documentation updated (outbound flow diagram, configuration, error codes)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
