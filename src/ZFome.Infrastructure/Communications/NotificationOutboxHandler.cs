using System.Text.Json;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Communications;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Infrastructure.Communications;

internal sealed class NotificationOutboxHandler(
    INotificationWriteRepository repository,
    IEnumerable<INotificationChannelSender> senders,
    NotificationRequestOutboxStager outbox,
    IOptions<NotificationDeliveryOptions> options,
    TimeProvider timeProvider) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string ConsumerName => "communications.notification-delivery";
    public string MessageType => NotificationRequestOutboxStager.MessageType;
    public int SchemaVersion => NotificationRequestOutboxStager.SchemaVersion;

    public async Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken)
    {
        var requested = JsonSerializer.Deserialize<NotificationRequested>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Notification outbox payload is invalid.");
        var notification = await repository.FindDeliveryAsync(message.TenantId, requested.NotificationId, cancellationToken)
            ?? throw new InvalidOperationException("Notification referenced by outbox message was not found.");
        if (notification.Status is NotificationDeliveryStatus.AcceptedByProvider or NotificationDeliveryStatus.BlockedByConsent
            or NotificationDeliveryStatus.Failed or NotificationDeliveryStatus.Uncertain)
            return;

        var now = timeProvider.GetUtcNow();
        if (notification.RequiresConsent && !await repository.HasConsentAsync(message.TenantId, notification.EstablishmentId,
                notification.Channel, notification.Purpose,
                NotificationWriteRepository.NormalizeDestination(notification.Channel, notification.Destination), cancellationToken))
        {
            await repository.RecordAttemptAsync(message.TenantId, notification.Id, NotificationDeliveryStatus.BlockedByConsent,
                null, "consent_required", now, cancellationToken);
            return;
        }

        var sender = senders.SingleOrDefault(item => item.Channel == notification.Channel)
            ?? throw new InvalidOperationException($"No sender is registered for notification channel {notification.Channel}.");
        var result = await sender.SendAsync(new NotificationChannelSendRequest(notification.Channel, notification.Destination,
            notification.Language, notification.Subject, notification.Body, notification.ProviderTemplateName,
            notification.Parameters, notification.IdempotencyKey), cancellationToken);
        var attemptNumber = notification.AttemptCount + 1;
        if (result.Outcome == NotificationProviderOutcome.RetryableFailure)
        {
            var settings = options.Value;
            if (attemptNumber < settings.MaximumAttempts)
            {
                var delay = Math.Min(settings.MaximumRetryDelaySeconds,
                    settings.InitialRetryDelaySeconds * Math.Pow(2, Math.Clamp(attemptNumber - 1, 0, 20)));
                await repository.RecordAttemptAsync(message.TenantId, notification.Id, NotificationDeliveryStatus.RetryScheduled,
                    result.ProviderMessageId, result.SafeErrorCode, now, cancellationToken);
                outbox.Stage(message.TenantId, notification.Id, $"notification:{notification.EstablishmentId:N}:{notification.Id:N}:retry:{attemptNumber + 1}",
                    now.AddSeconds(delay));
                return;
            }

            await repository.RecordAttemptAsync(message.TenantId, notification.Id, NotificationDeliveryStatus.Failed,
                result.ProviderMessageId, result.SafeErrorCode ?? "retry_limit_reached", now, cancellationToken);
            return;
        }

        var status = result.Outcome switch
        {
            NotificationProviderOutcome.Accepted => NotificationDeliveryStatus.AcceptedByProvider,
            NotificationProviderOutcome.PermanentFailure => NotificationDeliveryStatus.Failed,
            NotificationProviderOutcome.Uncertain => NotificationDeliveryStatus.Uncertain,
            _ => throw new InvalidOperationException("Notification provider returned an unsupported outcome.")
        };
        await repository.RecordAttemptAsync(message.TenantId, notification.Id, status, result.ProviderMessageId,
            result.SafeErrorCode, now, cancellationToken);
    }
}
