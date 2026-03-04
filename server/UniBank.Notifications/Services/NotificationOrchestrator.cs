using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniBank.Notifications.Configuration;
using UniBank.Notifications.Models;

namespace UniBank.Notifications.Services;

/// <summary>
/// Central orchestrator that processes notification requests: resolves templates,
/// checks rate limits and preferences, renders messages, sends via appropriate channels,
/// and handles retry logic with exponential backoff.
/// </summary>
public sealed class NotificationOrchestrator
{
    private readonly ISmsProvider _smsProvider;
    private readonly IPushNotificationProvider _pushProvider;
    private readonly INotificationTemplateStore _templateStore;
    private readonly INotificationPreferenceStore _preferenceStore;
    private readonly TemplateEngine _templateEngine;
    private readonly NotificationRateLimiter _rateLimiter;
    private readonly NotificationSettings _settings;
    private readonly ILogger<NotificationOrchestrator> _logger;

    public NotificationOrchestrator(
        ISmsProvider smsProvider,
        IPushNotificationProvider pushProvider,
        INotificationTemplateStore templateStore,
        INotificationPreferenceStore preferenceStore,
        TemplateEngine templateEngine,
        NotificationRateLimiter rateLimiter,
        IOptions<NotificationSettings> settings,
        ILogger<NotificationOrchestrator> logger)
    {
        _smsProvider = smsProvider;
        _pushProvider = pushProvider;
        _templateStore = templateStore;
        _preferenceStore = preferenceStore;
        _templateEngine = templateEngine;
        _rateLimiter = rateLimiter;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes a notification request: checks rate limits, loads templates,
    /// renders messages, and sends through the requested channels.
    /// </summary>
    public async Task SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        // Rate limit check (critical priority bypasses rate limiting)
        if (request.Priority != NotificationPriority.Critical && !_rateLimiter.IsAllowed(request.UserId))
        {
            _logger.LogWarning(
                "Notification rate-limited for user {UserId}, event {EventType}",
                request.UserId,
                request.EventType);
            return;
        }

        // Check user notification preferences
        var preferences = await _preferenceStore.GetPreferenceAsync(request.UserId, cancellationToken);

        foreach (var channel in request.Channels)
        {
            if (!IsChannelEnabledForUser(channel, preferences))
            {
                _logger.LogDebug(
                    "Channel {Channel} disabled for user {UserId}; skipping",
                    channel,
                    request.UserId);
                continue;
            }

            var template = await _templateStore.GetTemplateAsync(
                request.EventType,
                channel,
                request.TenantId,
                cancellationToken);

            if (template is null)
            {
                _logger.LogWarning(
                    "No active template found for event {EventType}, channel {Channel}",
                    request.EventType,
                    channel);
                continue;
            }

            var renderedTitle = template.TitleTemplate is not null
                ? _templateEngine.Render(template.TitleTemplate, request.Variables)
                : string.Empty;
            var renderedBody = _templateEngine.Render(template.BodyTemplate, request.Variables);

            var log = new NotificationLog
            {
                UserId = request.UserId,
                EventType = request.EventType,
                Channel = channel,
                Title = renderedTitle,
                Body = renderedBody
            };

            await SendWithRetryAsync(request, log, cancellationToken);
        }
    }

    private async Task SendWithRetryAsync(
        NotificationRequest request,
        NotificationLog log,
        CancellationToken cancellationToken)
    {
        var maxRetries = _settings.MaxRetryAttempts;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delayIndex = Math.Min(attempt - 1, _settings.RetryDelaySeconds.Length - 1);
                var delay = TimeSpan.FromSeconds(_settings.RetryDelaySeconds[delayIndex]);

                _logger.LogInformation(
                    "Retry {Attempt}/{MaxRetries} for {Channel} notification to user {UserId} in {Delay}s",
                    attempt,
                    maxRetries,
                    log.Channel,
                    request.UserId,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
                log.RetryCount = attempt;
            }

            try
            {
                var success = log.Channel switch
                {
                    NotificationChannel.Sms => await SendSmsAsync(request, log, cancellationToken),
                    NotificationChannel.Push => await SendPushAsync(request, log, cancellationToken),
                    _ => false
                };

                // If the channel was skipped (e.g., no phone/token), do not retry
                if (log.Status == NotificationStatus.Skipped)
                {
                    return;
                }

                if (success)
                {
                    log.Status = NotificationStatus.Sent;
                    log.SentAt = DateTimeOffset.UtcNow;

                    _logger.LogInformation(
                        "Sent {Channel} notification to user {UserId} for event {EventType}",
                        log.Channel,
                        request.UserId,
                        request.EventType);
                    return;
                }

                log.Status = NotificationStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send {Channel} notification to user {UserId} (attempt {Attempt})",
                    log.Channel,
                    request.UserId,
                    attempt + 1);

                log.Status = NotificationStatus.Failed;
                log.FailureReason = ex.Message;
            }
        }

        _logger.LogError(
            "All {MaxRetries} retry attempts exhausted for {Channel} notification to user {UserId}, event {EventType}",
            maxRetries,
            log.Channel,
            request.UserId,
            request.EventType);
    }

    private async Task<bool> SendSmsAsync(
        NotificationRequest request,
        NotificationLog log,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.PhoneNumber))
        {
            log.Status = NotificationStatus.Skipped;
            log.FailureReason = "No phone number available";
            _logger.LogDebug("Skipping SMS for user {UserId}: no phone number", request.UserId);
            return false;
        }

        return await _smsProvider.SendAsync(request.PhoneNumber, log.Body, cancellationToken);
    }

    private async Task<bool> SendPushAsync(
        NotificationRequest request,
        NotificationLog log,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.FcmToken))
        {
            log.Status = NotificationStatus.Skipped;
            log.FailureReason = "No FCM token registered";
            _logger.LogDebug("Skipping push for user {UserId}: no FCM token", request.UserId);
            return false;
        }

        return await _pushProvider.SendAsync(
            request.FcmToken,
            log.Title ?? string.Empty,
            log.Body,
            cancellationToken: cancellationToken);
    }

    private static bool IsChannelEnabledForUser(NotificationChannel channel, NotificationPreference preference)
    {
        return channel switch
        {
            NotificationChannel.Sms => preference.SmsEnabled,
            NotificationChannel.Push => preference.PushEnabled,
            _ => false
        };
    }
}
