# STORY-073: Notification Service - Push & SMS

**Epic:** Cross-cutting
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a system,
I want push notifications and SMS delivered reliably,
So that users receive timely transaction alerts.

---

## Description

### Background
In mobile money and digital banking for the unbanked, notifications are not just a convenience feature -- they are a critical trust mechanism. Users who cannot check balances frequently rely on transaction notifications to confirm that their money moved correctly. Missed notifications erode trust and drive users back to cash.

The UniBank Notification Service is a standalone service that subscribes to domain events published via Wolverine and delivers notifications through multiple channels: Firebase Cloud Messaging (FCM) for push notifications and configurable SMS gateways for text messages. Each event type maps to a notification template, and the service tracks delivery status for every notification sent.

The service must be resilient: if FCM is temporarily unavailable, push notifications are retried with exponential backoff. If SMS delivery fails, the system retries and eventually falls back to an alternative channel. Rate limiting per account prevents notification spam.

### Scope

**In scope:**
- Wolverine event handler subscriptions for all relevant domain events
- Firebase Cloud Messaging (FCM) integration for push notifications
- SMS gateway integration interface (configurable, with mock for development)
- Notification template engine with variable substitution
- `notification_templates` table for managing templates per event type and channel
- `notification_log` table for delivery tracking
- Retry mechanism with exponential backoff for failed deliveries
- Rate limiting per account (max notifications per time window)
- Multi-channel delivery: push + SMS for critical events, push only for informational events
- Channel preference support (user can opt out of non-essential channels)

**Out of scope:**
- Email notifications (future sprint)
- In-app notification center / inbox (future sprint)
- WhatsApp Business API integration (future sprint)
- Notification preference management UI (future sprint)
- Marketing/promotional notifications
- Notification analytics dashboard

### User Flow

**Event-Driven Notification Flow:**
1. A domain event occurs (e.g., `TransactionCompleted`)
2. Wolverine routes the event to the Notification Service handler
3. Handler looks up notification templates for the event type
4. Handler resolves the target account and retrieves FCM token and phone number
5. Handler substitutes template variables with event data
6. Handler sends push notification via FCM (if FCM token exists)
7. Handler sends SMS via SMS gateway (if event is SMS-eligible)
8. Handler logs delivery attempts to `notification_log` table
9. If delivery fails, handler schedules retry with exponential backoff
10. Delivery status is updated as notifications are confirmed delivered

**Example -- Transaction Completed:**
- Push notification: "You received ZAR 500.00 from John D. New balance: ZAR 1,500.00"
- SMS: "UniBank: Received R500.00 from John D. Bal: R1,500.00. Ref: TXN-ABC123"

---

## Acceptance Criteria

- [ ] Wolverine handlers are registered for: `TransactionCompleted`, `TransactionFailed`, `AccountCreated`, `UserRegistered`, `KYCApproved`, `KYCRejected`, `FraudAlertRaised`, `LowFloatAlert`
- [ ] Each handler resolves the target account, loads appropriate templates, and sends notifications
- [ ] FCM push notification integration sends messages to the account's registered FCM token
- [ ] SMS gateway integration sends messages to the account's phone number
- [ ] `notification_templates` table stores templates per `(event_type, channel)` combination
- [ ] Templates support variable substitution: `{amount}`, `{currency}`, `{counterparty_name}`, `{reference}`, `{balance}`, `{account_name}`
- [ ] `notification_log` table tracks: `id`, `account_id`, `event_type`, `channel`, `status`, `sent_at`, `delivered_at`, `failure_reason`, `retry_count`
- [ ] Failed deliveries are retried with exponential backoff: 30s, 2min, 10min (max 3 retries)
- [ ] Rate limiting prevents more than 20 notifications per account per hour
- [ ] Critical events (FraudAlertRaised, TransactionFailed) bypass rate limiting
- [ ] SMS is sent for all events; push is sent only if FCM token is registered
- [ ] Mock implementations exist for FCM and SMS gateways in development
- [ ] Unit tests cover template rendering, rate limiting, retry logic (>=80% coverage)
- [ ] Integration test demonstrates end-to-end event -> notification flow

---

## Technical Notes

### Components

**Project:** `UniBank.Notifications`

**File Structure:**
```
UniBank.Notifications/
  Program.cs
  appsettings.json
  Handlers/
    TransactionCompletedHandler.cs
    TransactionFailedHandler.cs
    AccountCreatedHandler.cs
    UserRegisteredHandler.cs
    KYCApprovedHandler.cs
    KYCRejectedHandler.cs
    FraudAlertHandler.cs
    LowFloatAlertHandler.cs
  Services/
    INotificationSender.cs
    NotificationOrchestrator.cs
    TemplateRenderer.cs
    RateLimiter.cs
  Channels/
    IPushNotificationService.cs
    FcmPushService.cs
    MockPushService.cs
    ISmsService.cs
    SmsGatewayService.cs
    MockSmsService.cs
  Models/
    NotificationTemplate.cs
    NotificationLog.cs
    NotificationRequest.cs
  Persistence/
    NotificationDbContext.cs
    NotificationTemplateConfiguration.cs
    NotificationLogConfiguration.cs
  Configuration/
    FcmSettings.cs
    SmsSettings.cs
    NotificationSettings.cs
```

### Event Handler Implementations

**TransactionCompletedHandler.cs:**
```csharp
public class TransactionCompletedHandler
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<TransactionCompletedHandler> _logger;

    public async Task Handle(TransactionCompleted @event)
    {
        _logger.LogInformation(
            "Processing TransactionCompleted notification for account {AccountId}, txn {TransactionId}",
            @event.AccountId, @event.TransactionId);

        var variables = new Dictionary<string, string>
        {
            ["amount"] = FormatMoney(@event.Amount, @event.Currency),
            ["currency"] = @event.Currency,
            ["transaction_type"] = FormatTransactionType(@event.TransactionType),
            ["reference"] = @event.Reference,
            ["counterparty_name"] = @event.CounterpartyName ?? "Unknown",
            ["balance"] = FormatMoney(@event.NewBalance, @event.Currency)
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            AccountId = @event.AccountId,
            TenantId = @event.TenantId,
            EventType = "TransactionCompleted",
            Variables = variables,
            Priority = NotificationPriority.Normal,
            Channels = new[] { "push", "sms" }
        });
    }

    private static string FormatMoney(decimal amount, string currency)
    {
        return currency switch
        {
            "ZAR" => $"R{amount:N2}",
            "USD" => $"${amount:N2}",
            _ => $"{currency} {amount:N2}"
        };
    }

    private static string FormatTransactionType(string type) => type switch
    {
        "p2p_send" => "Sent",
        "p2p_receive" => "Received",
        "cash_in" => "Cash In",
        "cash_out" => "Cash Out",
        "payment_nfc" => "NFC Payment",
        "payment_qr" => "QR Payment",
        "bill_payment" => "Bill Payment",
        _ => type
    };
}
```

**FraudAlertHandler.cs:**
```csharp
public class FraudAlertHandler
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<FraudAlertHandler> _logger;

    public async Task Handle(FraudAlertRaised @event)
    {
        _logger.LogWarning(
            "Processing FraudAlertRaised notification for account {AccountId}, severity {Severity}",
            @event.AccountId, @event.Severity);

        var variables = new Dictionary<string, string>
        {
            ["alert_type"] = @event.AlertType,
            ["description"] = @event.Description,
            ["severity"] = @event.Severity
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            AccountId = @event.AccountId,
            TenantId = @event.TenantId,
            EventType = "FraudAlertRaised",
            Variables = variables,
            Priority = NotificationPriority.Critical, // Bypasses rate limiting
            Channels = new[] { "push", "sms" }
        });
    }
}
```

### Notification Orchestrator

```csharp
public class NotificationOrchestrator : INotificationSender
{
    private readonly IPushNotificationService _pushService;
    private readonly ISmsService _smsService;
    private readonly TemplateRenderer _templateRenderer;
    private readonly RateLimiter _rateLimiter;
    private readonly NotificationDbContext _dbContext;
    private readonly TenantDbContext _tenantDb;
    private readonly ILogger<NotificationOrchestrator> _logger;

    public async Task SendNotificationAsync(NotificationRequest request)
    {
        // Rate limit check (skip for critical priority)
        if (request.Priority != NotificationPriority.Critical)
        {
            var isAllowed = await _rateLimiter.IsAllowedAsync(
                request.AccountId, request.TenantId);
            if (!isAllowed)
            {
                _logger.LogWarning(
                    "Notification rate limited for account {AccountId}",
                    request.AccountId);
                return;
            }
        }

        // Load account info for delivery
        var account = await _tenantDb.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AccountId);

        if (account == null)
        {
            _logger.LogWarning("Account {AccountId} not found for notification",
                request.AccountId);
            return;
        }

        foreach (var channel in request.Channels)
        {
            // Load template
            var template = await _dbContext.NotificationTemplates
                .FirstOrDefaultAsync(t =>
                    t.EventType == request.EventType &&
                    t.Channel == channel &&
                    t.IsActive);

            if (template == null)
            {
                _logger.LogWarning(
                    "No template found for event {EventType}, channel {Channel}",
                    request.EventType, channel);
                continue;
            }

            // Render template
            var renderedTitle = _templateRenderer.Render(
                template.TitleTemplate, request.Variables);
            var renderedBody = _templateRenderer.Render(
                template.BodyTemplate, request.Variables);

            // Create log entry
            var logEntry = new NotificationLog
            {
                Id = Guid.NewGuid(),
                AccountId = request.AccountId,
                EventType = request.EventType,
                Channel = channel,
                Title = renderedTitle,
                Body = renderedBody,
                Status = "pending",
                RetryCount = 0,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.NotificationLogs.Add(logEntry);
            await _dbContext.SaveChangesAsync();

            // Send via channel
            try
            {
                bool delivered = channel switch
                {
                    "push" => await SendPushAsync(account, renderedTitle, renderedBody, logEntry),
                    "sms" => await SendSmsAsync(account, renderedBody, logEntry),
                    _ => false
                };

                logEntry.Status = delivered ? "sent" : "failed";
                logEntry.SentAt = delivered ? DateTimeOffset.UtcNow : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send {Channel} notification for account {AccountId}",
                    channel, request.AccountId);
                logEntry.Status = "failed";
                logEntry.FailureReason = ex.Message;
            }

            await _dbContext.SaveChangesAsync();

            // Schedule retry if failed
            if (logEntry.Status == "failed" && logEntry.RetryCount < 3)
            {
                await ScheduleRetryAsync(logEntry);
            }
        }
    }

    private async Task<bool> SendPushAsync(
        Account account, string title, string body, NotificationLog log)
    {
        if (string.IsNullOrEmpty(account.FcmToken))
        {
            log.Status = "skipped";
            log.FailureReason = "No FCM token registered";
            return false;
        }

        return await _pushService.SendAsync(account.FcmToken, title, body);
    }

    private async Task<bool> SendSmsAsync(
        Account account, string body, NotificationLog log)
    {
        return await _smsService.SendAsync(account.Phone, body);
    }

    private async Task ScheduleRetryAsync(NotificationLog log)
    {
        var delays = new[] { 30, 120, 600 }; // seconds: 30s, 2min, 10min
        var delaySeconds = delays[Math.Min(log.RetryCount, delays.Length - 1)];

        _logger.LogInformation(
            "Scheduling retry #{RetryCount} for notification {NotificationId} in {Delay}s",
            log.RetryCount + 1, log.Id, delaySeconds);

        // Use Wolverine's delayed message feature for retry scheduling
        // This leverages the durable outbox for reliable retry
        // Implementation detail: publish a RetryNotification command with delay
    }
}
```

### Template Renderer

```csharp
public class TemplateRenderer
{
    private static readonly Regex VariablePattern = new(
        @"\{(\w+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Renders a template by substituting {variable} placeholders
    /// with values from the provided dictionary.
    /// </summary>
    public string Render(string template, IDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        return VariablePattern.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}
```

### Rate Limiter

```csharp
public class RateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly NotificationSettings _settings;

    public async Task<bool> IsAllowedAsync(Guid accountId, Guid tenantId)
    {
        var db = _redis.GetDatabase();
        var key = $"notification:ratelimit:{accountId}";
        var count = await db.StringIncrementAsync(key);

        if (count == 1)
            await db.KeyExpireAsync(key, TimeSpan.FromHours(1));

        return count <= _settings.MaxNotificationsPerAccountPerHour; // Default: 20
    }
}
```

### FCM Integration

```csharp
public interface IPushNotificationService
{
    Task<bool> SendAsync(string fcmToken, string title, string body,
        Dictionary<string, string>? data = null);
}

public class FcmPushService : IPushNotificationService
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FcmPushService> _logger;

    public async Task<bool> SendAsync(string fcmToken, string title, string body,
        Dictionary<string, string>? data = null)
    {
        var message = new Message
        {
            Token = fcmToken,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification
                {
                    ClickAction = "OPEN_APP",
                    Sound = "default"
                }
            },
            Data = data
        };

        try
        {
            var response = await _messaging.SendAsync(message);
            _logger.LogDebug("FCM sent: {MessageId}", response);
            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex, "FCM send failed: {ErrorCode}", ex.MessagingErrorCode);

            // Handle specific FCM errors
            if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                // Token is invalid; mark for removal
                _logger.LogWarning("FCM token unregistered, should be removed");
            }

            return false;
        }
    }
}

// Mock for development
public class MockPushService : IPushNotificationService
{
    private readonly ILogger<MockPushService> _logger;

    public Task<bool> SendAsync(string fcmToken, string title, string body,
        Dictionary<string, string>? data = null)
    {
        _logger.LogInformation(
            "[MOCK PUSH] Token: {Token}, Title: {Title}, Body: {Body}",
            fcmToken?[..Math.Min(20, fcmToken.Length)] + "...", title, body);
        return Task.FromResult(true);
    }
}
```

### API / gRPC Endpoints
The Notification Service does not expose gRPC endpoints. It is purely event-driven, consuming Wolverine domain events.

### Database Changes

**notification_templates table (tenant schema or public schema):**
```sql
CREATE TABLE notification_templates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type VARCHAR(50) NOT NULL,
    channel VARCHAR(20) NOT NULL CHECK (channel IN ('push', 'sms', 'email', 'in_app')),
    title_template TEXT,                 -- Push notification title template
    body_template TEXT NOT NULL,          -- Notification body template
    variables TEXT[] NOT NULL DEFAULT '{}', -- List of expected variables
    is_active BOOLEAN NOT NULL DEFAULT true,
    priority VARCHAR(20) NOT NULL DEFAULT 'normal'
        CHECK (priority IN ('low', 'normal', 'high', 'critical')),
    tenant_id UUID,                      -- NULL = default, non-null = tenant-specific override
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(event_type, channel, tenant_id)
);
```

**notification_log table (tenant schema):**
```sql
CREATE TABLE {schema}.notification_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES {schema}.accounts(id),
    event_type VARCHAR(50) NOT NULL,
    channel VARCHAR(20) NOT NULL,
    title VARCHAR(200),
    body TEXT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'sent', 'delivered', 'failed', 'skipped')),
    failure_reason TEXT,
    retry_count INT NOT NULL DEFAULT 0,
    sent_at TIMESTAMPTZ,
    delivered_at TIMESTAMPTZ,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes
CREATE INDEX idx_notification_log_account ON {schema}.notification_log (account_id, created_at DESC);
CREATE INDEX idx_notification_log_status ON {schema}.notification_log (status) WHERE status IN ('pending', 'failed');
```

**Seed Data -- Default Notification Templates:**

| event_type | channel | title_template | body_template |
|---|---|---|---|
| TransactionCompleted | push | `{transaction_type} {currency} {amount}` | `You {transaction_type} {amount} {counterparty_name}. Balance: {balance}. Ref: {reference}` |
| TransactionCompleted | sms | (none) | `UniBank: {transaction_type} {amount} {counterparty_name}. Bal: {balance}. Ref: {reference}` |
| TransactionFailed | push | `Transaction Failed` | `Your {transaction_type} of {amount} failed. Reason: {failure_reason}. Ref: {reference}` |
| TransactionFailed | sms | (none) | `UniBank: {transaction_type} {amount} failed. {failure_reason}. Ref: {reference}` |
| AccountCreated | push | `Welcome to UniBank` | `Your account has been created. Complete KYC to unlock all features.` |
| AccountCreated | sms | (none) | `Welcome to UniBank! Your account is ready. Complete KYC to unlock all features.` |
| KYCApproved | push | `KYC Approved` | `Your identity has been verified. You now have Level {kyc_level} access.` |
| KYCApproved | sms | (none) | `UniBank: KYC approved. Level {kyc_level} access granted.` |
| KYCRejected | push | `KYC Review Update` | `Your {document_type} was not accepted. Reason: {rejection_reason}. Please resubmit.` |
| KYCRejected | sms | (none) | `UniBank: Your {document_type} was not accepted. Please resubmit in the app.` |
| FraudAlertRaised | push | `Security Alert` | `Unusual activity detected on your account. {description}. Contact support if unauthorized.` |
| FraudAlertRaised | sms | (none) | `ALERT: Unusual activity on your UniBank account. {description}. Call support immediately if not you.` |
| LowFloatAlert | push | `Low Float Warning` | `Your float balance is {current_float}. Limit: {float_limit}. Please top up.` |
| LowFloatAlert | sms | (none) | `UniBank Agent: Low float ({current_float}/{float_limit}). Please top up.` |

### Security Considerations
- FCM server key must be stored as a secret (not in appsettings)
- SMS gateway credentials must be stored as secrets
- Notification bodies may contain financial information -- delivery tracking helps with auditing
- Push notifications should not contain full account numbers or balances in the notification preview (only when the user opens the app)
- Rate limiting prevents notification-based DoS on user devices
- FCM tokens should be validated and expired tokens cleaned up regularly
- SMS messages should be concise (within 160 characters for single SMS to minimize cost)
- Critical alerts (fraud) must bypass rate limiting and use both channels
- Notification logs should be retained per regulatory requirements (varies by country)

### Edge Cases
- FCM token expired or unregistered: Log and mark for cleanup; do not fail the entire notification flow
- SMS gateway temporarily unavailable: Retry with backoff; log all failures
- Account has no FCM token (never installed app or cleared data): Skip push, send SMS only
- Account phone number invalid (impossible after registration, but defensive): Log and skip SMS
- Template not found for an event type: Log warning and skip (do not fail the event handler)
- Variable missing from event data: Template renderer leaves `{variable}` placeholder as-is; logged as warning
- High event volume (e.g., batch settlement): Rate limiter protects users; consider batching for agents
- Event handler throws exception: Wolverine retries per configured policy; dead-lettered after max retries
- Duplicate event delivery (Wolverine at-least-once): Notification log `event_id` deduplication prevents duplicate notifications
- Multi-tenant template override: Tenant-specific templates override defaults; fall back to default if no override exists

---

## Dependencies

**Prerequisite Stories:**
- STORY-007: Wolverine Messaging & MQTT Broker Configuration (Wolverine must be configured for event routing)
- STORY-003: PostgreSQL Database Schema (notification tables in tenant schema)

**Blocked Stories:**
- No stories are directly blocked, but notification delivery is critical for user trust and should be operational before transaction features go live

**External Dependencies:**
- Firebase project with Cloud Messaging enabled (FCM)
- `FirebaseAdmin` NuGet package for FCM
- SMS gateway provider (Africa's Talking, Twilio, Clickatell, or similar) -- mock for development
- Redis (for rate limiting)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) for template renderer, rate limiter, handlers
- [ ] Integration tests passing (Wolverine event -> notification handler -> mock delivery)
- [ ] Code reviewed and approved
- [ ] Documentation updated (notification template catalog, channel configuration guide)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
