# STORY-007: Wolverine Messaging & MQTT Broker Configuration

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a developer,
I want Wolverine messaging and MQTT broker configured,
So that services can communicate asynchronously.

---

## Description

### Background
UniBank requires robust asynchronous messaging for two distinct communication patterns:

1. **Domain Event Messaging (Wolverine):** Internal service-to-service communication within the Modular Monolith (Core Banking) and between satellite services. Events like `AccountCreated`, `TransactionCompleted`, and `FraudAlertRaised` must be reliably published and consumed. Wolverine provides a durable outbox pattern backed by PostgreSQL, guaranteeing that events are not lost even if a service crashes mid-transaction.

2. **IoT/Terminal Communication (MQTT):** POS terminals communicate with the Terminal Manager via MQTT, a lightweight publish/subscribe protocol ideal for low-bandwidth, unreliable network connections common in Southern African rural areas. An embedded MQTT broker (MQTTnet) in the Terminal Manager handles terminal heartbeats, command distribution, firmware updates, and key injection.

This story sets up both messaging systems, defines the domain event contracts in SharedKernel, configures Wolverine handlers, and implements the MQTT broker with topic structure.

### Scope

**In scope:**
- Wolverine configuration in Core Banking with PostgreSQL-backed durable outbox
- Domain event definitions in SharedKernel
- Sample Wolverine message handlers to verify end-to-end delivery
- Wolverine saga support configuration for multi-step flows
- MQTT broker (MQTTnet) embedded in Terminal Manager service
- MQTT topic structure and access control
- Integration test verifying Wolverine message delivery
- Integration test verifying MQTT publish/subscribe

**Out of scope:**
- Implementation of all business-logic event handlers (handled in feature stories)
- MQTT over WebSocket support
- External MQTT broker (Mosquitto, EMQX) -- using embedded MQTTnet for simplicity
- Message replay or event sourcing
- Dead letter queue UI/admin tooling
- Cross-border message routing

### User Flow

**Wolverine Flow:**
1. A domain operation occurs (e.g., user registers)
2. The command handler publishes a domain event (e.g., `UserRegistered`) via Wolverine
3. Wolverine stores the event in the PostgreSQL outbox within the same transaction
4. Wolverine's outbox agent polls for unsent messages and dispatches them
5. Subscribed handlers receive and process the event (e.g., NotificationService sends welcome SMS)
6. If a handler fails, Wolverine retries with configurable backoff
7. After max retries, the message moves to the dead letter queue

**MQTT Flow:**
1. POS terminal connects to MQTT broker on Terminal Manager (port 1883)
2. Terminal subscribes to its command topic: `terminals/{terminal_id}/commands`
3. Terminal publishes heartbeat to: `terminals/{terminal_id}/status`
4. Terminal Manager receives heartbeat and updates terminal status in database
5. When admin pushes a firmware update, Terminal Manager publishes to: `terminals/{terminal_id}/updates`
6. Terminal receives update command and begins firmware download

---

## Acceptance Criteria

- [ ] Wolverine is configured in Core Banking with PostgreSQL-backed durable outbox
- [ ] Wolverine outbox uses the same PostgreSQL database as the application (co-located for transactional consistency)
- [ ] Domain events are defined in `UniBank.SharedKernel/Events/`: `AccountCreated`, `UserRegistered`, `TransactionCompleted`, `TransactionFailed`, `KYCApproved`, `KYCRejected`, `FraudAlertRaised`, `LowFloatAlert`, `TerminalStatusChanged`, `PINCreated`
- [ ] All domain events inherit from a common `DomainEvent` base class with `EventId`, `OccurredAt`, `TenantId` properties
- [ ] A sample Wolverine handler exists and successfully processes a test event
- [ ] Wolverine retry policy is configured: 3 retries with exponential backoff (1s, 5s, 30s)
- [ ] Dead letter queue is configured for messages that exhaust retries
- [ ] Wolverine saga base class is configured for multi-step transaction orchestration
- [ ] MQTT broker (MQTTnet) is embedded in Terminal Manager and listens on port 1883
- [ ] MQTT topics are structured as: `terminals/{id}/status`, `terminals/{id}/commands`, `terminals/{id}/updates`, `terminals/{id}/keys`
- [ ] MQTT broker validates client authentication (terminal_id + API key)
- [ ] Integration test publishes a Wolverine event and verifies handler receives it
- [ ] Integration test connects an MQTT client, publishes to a topic, and verifies subscription receives the message

---

## Technical Notes

### Components

**Affected Projects:**
- `UniBank.SharedKernel` -- Domain event base classes and event definitions
- `UniBank.Core` -- Wolverine configuration, outbox setup, sample handlers
- `UniBank.TerminalManager` -- MQTT broker (MQTTnet) configuration
- `UniBank.Notifications` -- Wolverine handler subscriptions (wiring only, handlers implemented in STORY-073)

### Domain Event Definitions (SharedKernel)

```csharp
// UniBank.SharedKernel/Events/DomainEvent.cs
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public Guid TenantId { get; init; }
    public string? CorrelationId { get; init; }
}

// UniBank.SharedKernel/Events/UserRegistered.cs
public record UserRegistered : DomainEvent
{
    public Guid AccountId { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
}

// UniBank.SharedKernel/Events/AccountCreated.cs
public record AccountCreated : DomainEvent
{
    public Guid AccountId { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string Status { get; init; } = "pending_kyc";
}

// UniBank.SharedKernel/Events/PINCreated.cs
public record PINCreated : DomainEvent
{
    public Guid AccountId { get; init; }
}

// UniBank.SharedKernel/Events/TransactionCompleted.cs
public record TransactionCompleted : DomainEvent
{
    public Guid TransactionId { get; init; }
    public Guid AccountId { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Fee { get; init; }
    public string Currency { get; init; } = "ZAR";
    public string Reference { get; init; } = string.Empty;
    public string? CounterpartyName { get; init; }
    public decimal NewBalance { get; init; }
}

// UniBank.SharedKernel/Events/TransactionFailed.cs
public record TransactionFailed : DomainEvent
{
    public Guid TransactionId { get; init; }
    public Guid AccountId { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string FailureReason { get; init; } = string.Empty;
}

// UniBank.SharedKernel/Events/KYCApproved.cs
public record KYCApproved : DomainEvent
{
    public Guid AccountId { get; init; }
    public int NewKYCLevel { get; init; }
    public Guid ReviewedBy { get; init; }
}

// UniBank.SharedKernel/Events/KYCRejected.cs
public record KYCRejected : DomainEvent
{
    public Guid AccountId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string RejectionReason { get; init; } = string.Empty;
    public Guid ReviewedBy { get; init; }
}

// UniBank.SharedKernel/Events/FraudAlertRaised.cs
public record FraudAlertRaised : DomainEvent
{
    public Guid AccountId { get; init; }
    public Guid? TransactionId { get; init; }
    public string AlertType { get; init; } = string.Empty; // velocity, geo, amount_threshold
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = "medium"; // low, medium, high, critical
}

// UniBank.SharedKernel/Events/LowFloatAlert.cs
public record LowFloatAlert : DomainEvent
{
    public Guid AgentId { get; init; }
    public decimal CurrentFloat { get; init; }
    public decimal FloatLimit { get; init; }
    public decimal ThresholdPercentage { get; init; }
}

// UniBank.SharedKernel/Events/TerminalStatusChanged.cs
public record TerminalStatusChanged : DomainEvent
{
    public Guid TerminalId { get; init; }
    public Guid MerchantId { get; init; }
    public string PreviousStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public string? Reason { get; init; }
}
```

### Wolverine Configuration (Core Banking)

```csharp
// UniBank.Core/Program.cs or extension method
builder.Host.UseWolverine(opts =>
{
    // PostgreSQL-backed durable outbox
    opts.PersistMessagesWithPostgresql(
        builder.Configuration.GetConnectionString("PostgreSQL")!,
        "wolverine" // schema name
    );

    // Enable durable outbox
    opts.Policies.UseDurableLocalQueues();
    opts.Policies.UseDurableOutbox();

    // Global retry policy
    opts.Policies.OnException<Exception>()
        .RetryWithCooldown(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30))
        .Then.MoveToErrorQueue();

    // Discover handlers in Core Banking assembly
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    // Also discover handlers in Notifications assembly
    opts.Discovery.IncludeAssembly(typeof(UniBank.Notifications.NotificationHandlers).Assembly);

    // Configure local queues
    opts.LocalQueue("notifications")
        .UseDurableInbox()
        .MaximumParallelMessages(5);

    opts.LocalQueue("audit")
        .UseDurableInbox()
        .MaximumParallelMessages(10);

    opts.LocalQueue("transactions")
        .UseDurableInbox()
        .Sequential(); // Process in order
});
```

### Wolverine Saga Example

```csharp
// UniBank.Core/Modules/Payments/Application/Sagas/PaymentSaga.cs
public class PaymentSaga : Saga
{
    public Guid PaymentId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "initiated";

    // Start the saga
    public static PaymentSaga Start(InitiatePaymentCommand command)
    {
        return new PaymentSaga
        {
            PaymentId = command.PaymentId,
            AccountId = command.AccountId,
            Amount = command.Amount,
            Status = "debit_pending"
        };
    }

    // Handle debit confirmation
    public void Handle(DebitConfirmed @event)
    {
        Status = "credit_pending";
        // Return command to credit merchant
    }

    // Handle credit confirmation
    public void Handle(CreditConfirmed @event)
    {
        Status = "completed";
        MarkCompleted(); // End the saga
    }

    // Handle timeout
    public void Handle(PaymentTimeout @event)
    {
        Status = "failed";
        // Publish compensation event
        MarkCompleted();
    }
}
```

### Sample Handler (Verification)

```csharp
// UniBank.Core/Handlers/SampleEventHandler.cs
public class AccountCreatedHandler
{
    private readonly ILogger<AccountCreatedHandler> _logger;

    public AccountCreatedHandler(ILogger<AccountCreatedHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(AccountCreated @event)
    {
        _logger.LogInformation(
            "Account created: {AccountId} for tenant {TenantId}",
            @event.AccountId, @event.TenantId);

        // In production, this would trigger welcome flows,
        // audit logging, etc.
        await Task.CompletedTask;
    }
}
```

### MQTT Broker Configuration (Terminal Manager)

```csharp
// UniBank.TerminalManager/Mqtt/MqttBrokerService.cs
public class MqttBrokerService : BackgroundService
{
    private readonly MqttServer _mqttServer;
    private readonly ILogger<MqttBrokerService> _logger;
    private readonly ITerminalAuthenticator _authenticator;

    public MqttBrokerService(
        ILogger<MqttBrokerService> logger,
        ITerminalAuthenticator authenticator)
    {
        _logger = logger;
        _authenticator = authenticator;

        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpointPort(1883)
            .WithDefaultEndpoint()
            .Build();

        _mqttServer = new MqttFactory().CreateMqttServer(options);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Validate client connections
        _mqttServer.ValidatingConnectionAsync += async e =>
        {
            var isValid = await _authenticator.ValidateAsync(
                e.ClientId, e.UserName, e.Password);

            if (!isValid)
            {
                e.ReasonCode = MqttConnectReasonCode.NotAuthorized;
                _logger.LogWarning("MQTT auth failed for client: {ClientId}", e.ClientId);
            }
        };

        // Handle subscriptions (restrict topics per terminal)
        _mqttServer.InterceptingSubscriptionAsync += async e =>
        {
            var allowedPrefix = $"terminals/{e.ClientId}/";
            if (!e.TopicFilter.Topic.StartsWith(allowedPrefix))
            {
                e.ProcessSubscription = false;
                _logger.LogWarning(
                    "MQTT subscription denied: {ClientId} -> {Topic}",
                    e.ClientId, e.TopicFilter.Topic);
            }
            await Task.CompletedTask;
        };

        // Handle incoming messages (heartbeats, status updates)
        _mqttServer.InterceptingPublishAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(
                e.ApplicationMessage.PayloadSegment);

            _logger.LogDebug(
                "MQTT message from {ClientId} on {Topic}",
                e.ClientId, topic);

            if (topic.EndsWith("/status"))
            {
                await HandleTerminalHeartbeat(e.ClientId, payload);
            }

            await Task.CompletedTask;
        };

        // Handle client disconnections
        _mqttServer.ClientDisconnectedAsync += async e =>
        {
            _logger.LogInformation(
                "MQTT client disconnected: {ClientId}, Reason: {Reason}",
                e.ClientId, e.DisconnectType);

            await HandleTerminalDisconnect(e.ClientId);
        };

        await _mqttServer.StartAsync();
        _logger.LogInformation("MQTT Broker started on port 1883");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _mqttServer.StopAsync();
        _logger.LogInformation("MQTT Broker stopped");
    }

    private async Task HandleTerminalHeartbeat(string terminalId, string payload)
    {
        // Parse heartbeat payload and update terminal status in database
        // Publish TerminalStatusChanged event via Wolverine if status changed
    }

    private async Task HandleTerminalDisconnect(string terminalId)
    {
        // Update terminal status to 'offline'
        // Publish TerminalStatusChanged event
    }
}
```

### MQTT Topic Structure

| Topic Pattern | Direction | Purpose | QoS |
|---|---|---|---|
| `terminals/{id}/status` | Terminal -> Server | Heartbeat, battery, signal strength | QoS 0 (at most once) |
| `terminals/{id}/commands` | Server -> Terminal | Operational commands (reboot, reset) | QoS 1 (at least once) |
| `terminals/{id}/updates` | Server -> Terminal | Firmware/config update notifications | QoS 2 (exactly once) |
| `terminals/{id}/keys` | Server -> Terminal | Cryptographic key injection | QoS 2 (exactly once) |

**Heartbeat Payload Example:**
```json
{
  "terminal_id": "term-001",
  "timestamp": "2026-02-24T10:30:00Z",
  "status": "active",
  "battery_level": 85,
  "signal_strength": -67,
  "firmware_version": "2.1.0",
  "pending_transactions": 0,
  "uptime_seconds": 86400
}
```

### API / gRPC Endpoints
No new gRPC endpoints. Wolverine messaging is internal. MQTT operates on port 1883.

### Database Changes
Wolverine creates its own schema (`wolverine`) in the PostgreSQL database for outbox and inbox tables. This is managed automatically by Wolverine's migration tooling.

Additional tables created by Wolverine:
- `wolverine.incoming_envelopes` -- durable inbox
- `wolverine.outgoing_envelopes` -- durable outbox
- `wolverine.dead_letters` -- dead letter queue

### Security Considerations
- MQTT broker must authenticate terminals using terminal_id and pre-shared API key
- MQTT topic subscriptions must be restricted per terminal (a terminal can only subscribe to its own topics)
- MQTT `keys` topic carries sensitive cryptographic material -- must use QoS 2 and consider additional payload encryption
- Wolverine messages may contain PII -- ensure PostgreSQL outbox data is encrypted at rest
- Domain events should not contain raw PINs or full phone numbers
- Dead letter messages should be monitored for sensitive data accumulation

### Edge Cases
- Wolverine outbox agent failure: Messages will accumulate in outbox table and be sent when agent recovers
- PostgreSQL connection loss: Wolverine should handle reconnection gracefully with backoff
- MQTT client reconnection: Terminals in rural areas may have intermittent connectivity; broker should handle frequent connect/disconnect
- MQTT message ordering: QoS 2 messages for key injection must be processed in order
- Saga timeout: Long-running sagas should have configurable timeout after which they are compensated
- Event handler exception: Wolverine retries per configured policy, then dead-letters the message
- High message volume: Monitor Wolverine queue depth; alert if processing falls behind
- Terminal ID spoofing: MQTT authentication prevents unauthorized terminals from subscribing to other terminals' topics
- Duplicate message delivery: Handlers must be idempotent (Wolverine guarantees at-least-once delivery)

---

## Dependencies

**Prerequisite Stories:**
- STORY-001: Solution Scaffolding & Project Structure (projects must exist)
- STORY-003: PostgreSQL Database Schema (Wolverine outbox needs PostgreSQL, domain events reference tenant concepts)

**Blocked Stories:**
- STORY-009: User Self-Registration (publishes `UserRegistered` and `AccountCreated` events)
- STORY-010: Create Account PIN (publishes `PINCreated` event)
- STORY-073: Notification Service (subscribes to domain events via Wolverine handlers)
- All stories that publish or consume domain events

**External Dependencies:**
- PostgreSQL instance (for Wolverine outbox storage)
- `Wolverine` and `Wolverine.EntityFrameworkCore` NuGet packages
- `MQTTnet` NuGet package (v4+)
- No external message broker required (Wolverine uses PostgreSQL, MQTT is embedded)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) for event definitions, MQTT authentication
- [ ] Integration tests passing (Wolverine end-to-end, MQTT pub/sub)
- [ ] Code reviewed and approved
- [ ] Documentation updated (domain event catalog, MQTT topic reference, Wolverine configuration guide)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
