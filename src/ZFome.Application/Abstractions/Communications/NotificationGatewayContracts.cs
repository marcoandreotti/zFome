using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Tenancy;
using ZFome.Domain.Ordering;

namespace ZFome.Application.Abstractions.Communications;

public enum NotificationChannel
{
    Email = 1,
    WhatsApp = 2
}

public enum NotificationDeliveryStatus
{
    Queued = 1,
    AcceptedByProvider = 2,
    BlockedByConsent = 3,
    RetryScheduled = 4,
    Failed = 5,
    Uncertain = 6
}

public sealed record NotificationTemplateDraft(
    Guid? Id,
    string Purpose,
    NotificationChannel Channel,
    string Language,
    string Subject,
    string Body,
    string? ProviderTemplateName,
    bool RequiresConsent,
    bool IsActive);

public sealed record NotificationTemplateView(
    Guid Id,
    string Purpose,
    NotificationChannel Channel,
    string Language,
    string Subject,
    string Body,
    string? ProviderTemplateName,
    bool RequiresConsent,
    bool IsActive,
    DateTimeOffset UpdatedAtUtc);

public sealed record NotificationConsentDraft(
    NotificationChannel Channel,
    string Purpose,
    string Destination,
    bool IsGranted,
    DateTimeOffset? CapturedAtUtc,
    string? Source);

public sealed record NotificationConsentView(
    Guid Id,
    NotificationChannel Channel,
    string Purpose,
    string Destination,
    bool IsGranted,
    DateTimeOffset CapturedAtUtc,
    string? Source);

public sealed record NotificationRequestDraft(
    Guid EstablishmentId,
    Guid TemplateId,
    string Destination,
    IReadOnlyDictionary<string, string> Parameters,
    string IdempotencyKey);

public sealed record NotificationRequestResult(Guid Id, bool Created);

public static class CustomerOrderNotificationPurposes
{
    public const string Confirmed = "order.confirmed";
    public const string Preparing = "order.preparing";
    public const string Ready = "order.ready";
    public const string OutForDelivery = "order.out_for_delivery";
    public const string Completed = "order.completed";
    public const string Cancelled = "order.cancelled";
    public const string Rejected = "order.rejected";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Confirmed, Preparing, Ready, OutForDelivery, Completed, Cancelled, Rejected
    };
}

public static class CustomerOrderNotificationParameters
{
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "name", "orderNumber", "status", "statusLabel", "trackingReference", "trackingUrl", "total", "serviceType"
    };
}

public interface ICustomerOrderNotificationStager
{
    Task StageAsync(Order order, CancellationToken cancellationToken);
}

public sealed record NotificationHistoryView(
    Guid Id,
    NotificationChannel Channel,
    string Purpose,
    string Destination,
    NotificationDeliveryStatus Status,
    string? ProviderMessageId,
    int AttemptCount,
    string? LastError,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record NotificationAttemptView(
    int AttemptNumber,
    NotificationDeliveryStatus Status,
    string? ProviderMessageId,
    string? SafeErrorCode,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationDelivery(
    Guid Id,
    Guid TenantId,
    Guid EstablishmentId,
    Guid TemplateId,
    NotificationChannel Channel,
    string Purpose,
    string Destination,
    string Language,
    string Subject,
    string Body,
    string? ProviderTemplateName,
    bool RequiresConsent,
    IReadOnlyDictionary<string, string> Parameters,
    string IdempotencyKey,
    NotificationDeliveryStatus Status,
    int AttemptCount);

public enum NotificationProviderOutcome
{
    Accepted = 1,
    RetryableFailure = 2,
    PermanentFailure = 3,
    Uncertain = 4
}

public sealed record NotificationProviderResult(
    NotificationProviderOutcome Outcome,
    string? ProviderMessageId,
    string? SafeErrorCode);

public sealed record NotificationChannelSendRequest(
    NotificationChannel Channel,
    string Destination,
    string Language,
    string Subject,
    string Body,
    string? ProviderTemplateName,
    IReadOnlyDictionary<string, string> Parameters,
    string IdempotencyKey);

public interface INotificationChannelSender
{
    NotificationChannel Channel { get; }
    Task<NotificationProviderResult> SendAsync(NotificationChannelSendRequest request, CancellationToken cancellationToken);
}

public interface INotificationWriteRepository
{
    Task<NotificationTemplateView?> FindTemplateAsync(OperationalScope scope, Guid templateId, CancellationToken cancellationToken);
    Task<Guid> UpsertTemplateAsync(OperationalScope scope, NotificationTemplateDraft template, DateTimeOffset now, CancellationToken cancellationToken);
    Task SetConsentAsync(OperationalScope scope, NotificationConsentDraft consent, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> HasConsentAsync(Guid tenantId, Guid establishmentId, NotificationChannel channel, string purpose, string normalizedDestination, CancellationToken cancellationToken);
    Task<NotificationRequestResult> RequestAsync(OperationalScope scope, NotificationRequestDraft request, NotificationTemplateView template, string parametersJson, DateTimeOffset now, CancellationToken cancellationToken);
    Task<NotificationDelivery?> FindDeliveryAsync(Guid tenantId, Guid notificationId, CancellationToken cancellationToken);
    Task RecordAttemptAsync(Guid tenantId, Guid notificationId, NotificationDeliveryStatus status, string? providerMessageId, string? safeErrorCode, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface INotificationReadGateway
{
    Task<IReadOnlyList<NotificationTemplateView>> ListTemplatesAsync(OperationalScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationConsentView>> ListConsentsAsync(OperationalScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationHistoryView>> ListHistoryAsync(OperationalScope scope, int page, int pageSize, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationAttemptView>> ListAttemptsAsync(OperationalScope scope, Guid notificationId, CancellationToken cancellationToken);
}

public sealed record NotificationRequested(Guid NotificationId);

public sealed class NotificationRequestOutboxStager(IOutboxMessageStager outbox, TimeProvider timeProvider)
{
    public const string MessageType = "communications.notification.requested";
    public const int SchemaVersion = 1;

    public void Stage(Guid tenantId, Guid notificationId, string idempotencyKey, DateTimeOffset? notBeforeUtc = null)
    {
        var now = timeProvider.GetUtcNow();
        outbox.Stage(new OutboxMessageDraft(
            tenantId,
            MessageType,
            SchemaVersion,
            idempotencyKey,
            now,
            System.Text.Json.JsonSerializer.Serialize(new NotificationRequested(notificationId)),
            notBeforeUtc));
    }
}
