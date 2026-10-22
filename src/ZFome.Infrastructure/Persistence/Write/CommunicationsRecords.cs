using ZFome.Application.Abstractions.Communications;

namespace ZFome.Infrastructure.Persistence.Write;

internal sealed class NotificationTemplateRecord
{
    private NotificationTemplateRecord() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public string Language { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? ProviderTemplateName { get; private set; }
    public bool RequiresConsent { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public NotificationTemplateRecord(Guid id, Guid tenantId, Guid establishmentId, NotificationTemplateDraft draft, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; EstablishmentId = establishmentId;
        Update(draft, now);
    }

    public void Update(NotificationTemplateDraft draft, DateTimeOffset now)
    {
        Purpose = draft.Purpose.Trim(); Channel = draft.Channel; Language = draft.Language.Trim();
        Subject = draft.Subject; Body = draft.Body; ProviderTemplateName = draft.ProviderTemplateName?.Trim();
        RequiresConsent = draft.RequiresConsent; IsActive = draft.IsActive; UpdatedAtUtc = now;
    }
}

internal sealed class NotificationConsentRecord
{
    private NotificationConsentRecord() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public string NormalizedDestination { get; private set; } = string.Empty;
    public bool IsGranted { get; private set; }
    public DateTimeOffset CapturedAtUtc { get; private set; }
    public string? Source { get; private set; }

    public NotificationConsentRecord(Guid id, Guid tenantId, Guid establishmentId, NotificationConsentDraft draft, string normalizedDestination, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; EstablishmentId = establishmentId; Channel = draft.Channel;
        Purpose = draft.Purpose.Trim(); Destination = draft.Destination.Trim(); NormalizedDestination = normalizedDestination;
        IsGranted = draft.IsGranted; CapturedAtUtc = draft.CapturedAtUtc ?? now; Source = draft.Source?.Trim();
    }

}

internal sealed class NotificationRecord
{
    private NotificationRecord() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid TemplateId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;
    public string NormalizedDestination { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? ProviderTemplateName { get; private set; }
    public bool RequiresConsent { get; private set; }
    public string ParametersJson { get; private set; } = "{}";
    public string IdempotencyKey { get; private set; } = string.Empty;
    public NotificationDeliveryStatus Status { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public NotificationRecord(Guid id, Guid tenantId, Guid establishmentId, NotificationTemplateView template,
        NotificationRequestDraft request, string normalizedDestination, string parametersJson, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; EstablishmentId = establishmentId; TemplateId = template.Id;
        Channel = template.Channel; Purpose = template.Purpose; Destination = request.Destination.Trim();
        NormalizedDestination = normalizedDestination; Language = template.Language; Subject = template.Subject;
        Body = template.Body; ProviderTemplateName = template.ProviderTemplateName; RequiresConsent = template.RequiresConsent;
        ParametersJson = parametersJson; IdempotencyKey = request.IdempotencyKey.Trim(); Status = NotificationDeliveryStatus.Queued;
        CreatedAtUtc = now; UpdatedAtUtc = now;
    }

    public void RecordAttempt(NotificationDeliveryStatus status, string? providerMessageId, string? safeErrorCode, DateTimeOffset now)
    {
        Status = status; ProviderMessageId = providerMessageId ?? ProviderMessageId; LastError = safeErrorCode;
        AttemptCount++; UpdatedAtUtc = now;
    }
}

internal sealed class NotificationAttemptRecord
{
    private NotificationAttemptRecord() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid NotificationId { get; private set; }
    public int AttemptNumber { get; private set; }
    public NotificationDeliveryStatus Status { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? SafeErrorCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public NotificationAttemptRecord(Guid id, Guid tenantId, Guid establishmentId, Guid notificationId,
        int attemptNumber, NotificationDeliveryStatus status, string? providerMessageId, string? safeErrorCode, DateTimeOffset now)
    {
        Id = id; TenantId = tenantId; EstablishmentId = establishmentId; NotificationId = notificationId;
        AttemptNumber = attemptNumber; Status = status; ProviderMessageId = providerMessageId; SafeErrorCode = safeErrorCode; CreatedAtUtc = now;
    }
}
