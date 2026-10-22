using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ZFome.Application.Abstractions.Communications;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;

namespace ZFome.Infrastructure.Persistence.Write;

internal sealed class NotificationWriteRepository(ZFomeDbContext context, NotificationRequestOutboxStager outbox) : INotificationWriteRepository
{
    public async Task<NotificationTemplateView?> FindTemplateAsync(OperationalScope scope, Guid templateId, CancellationToken cancellationToken)
    {
        var template = await context.NotificationTemplates.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == scope.TenantId && x.EstablishmentId == scope.EstablishmentId && x.Id == templateId,
            cancellationToken);
        return template is null ? null : Map(template);
    }

    public async Task<Guid> UpsertTemplateAsync(OperationalScope scope, NotificationTemplateDraft draft, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = draft.Id is { } id
            ? await context.NotificationTemplates.SingleOrDefaultAsync(x => x.TenantId == scope.TenantId && x.EstablishmentId == scope.EstablishmentId && x.Id == id, cancellationToken)
            : await context.NotificationTemplates.SingleOrDefaultAsync(x => x.TenantId == scope.TenantId && x.EstablishmentId == scope.EstablishmentId && x.Channel == draft.Channel && x.Purpose == draft.Purpose && x.Language == draft.Language, cancellationToken);
        if (draft.Id is not null && existing is null)
            throw new NotFoundException("Notification template was not found.");
        if (existing is null)
        {
            existing = new NotificationTemplateRecord(draft.Id ?? Guid.NewGuid(), scope.TenantId, scope.EstablishmentId, draft, now);
            await context.NotificationTemplates.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Update(draft, now);
        }

        await context.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }

    public async Task SetConsentAsync(OperationalScope scope, NotificationConsentDraft draft, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalized = NormalizeDestination(draft.Channel, draft.Destination);
        var record = new NotificationConsentRecord(Guid.NewGuid(), scope.TenantId, scope.EstablishmentId, draft, normalized, now);
        await context.NotificationConsents.AddAsync(record, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HasConsentAsync(Guid tenantId, Guid establishmentId, NotificationChannel channel, string purpose, string normalizedDestination, CancellationToken cancellationToken) =>
        await context.NotificationConsents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Channel == channel
                && x.Purpose == purpose && x.NormalizedDestination == normalizedDestination)
            .OrderByDescending(x => x.CapturedAtUtc).ThenByDescending(x => x.Id)
            .Select(x => (bool?)x.IsGranted).FirstOrDefaultAsync(cancellationToken) ?? false;

    public async Task<NotificationRequestResult> RequestAsync(OperationalScope scope, NotificationRequestDraft request, NotificationTemplateView template, string parametersJson, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var key = request.IdempotencyKey.Trim();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({scope.TenantId.ToString() + scope.EstablishmentId + key}, 0))",
            cancellationToken);
        var existing = await context.Notifications.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == scope.TenantId && x.EstablishmentId == scope.EstablishmentId && x.IdempotencyKey == key,
            cancellationToken);
        if (existing is not null)
        {
            var sameParameters = JsonNode.DeepEquals(JsonNode.Parse(existing.ParametersJson), JsonNode.Parse(parametersJson));
            if (existing.TemplateId != template.Id || existing.Destination != request.Destination.Trim() || !sameParameters)
                throw new ConflictException("An idempotency key cannot be reused for a different notification request.");
            await transaction.CommitAsync(cancellationToken);
            return new NotificationRequestResult(existing.Id, false);
        }

        var id = Guid.NewGuid();
        var normalized = NormalizeDestination(template.Channel, request.Destination);
        var notification = new NotificationRecord(id, scope.TenantId, scope.EstablishmentId, template, request, normalized, parametersJson, now);
        await context.Notifications.AddAsync(notification, cancellationToken);
        outbox.Stage(scope.TenantId, id, $"notification:{id:N}");
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new NotificationRequestResult(id, true);
    }

    public async Task<NotificationDelivery?> FindDeliveryAsync(Guid tenantId, Guid notificationId, CancellationToken cancellationToken)
    {
        var row = await context.Notifications.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == notificationId, cancellationToken);
        if (row is null) return null;
        var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ParametersJson) ?? [];
        return new NotificationDelivery(row.Id, row.TenantId, row.EstablishmentId, row.TemplateId, row.Channel, row.Purpose,
            row.Destination, row.Language, row.Subject, row.Body, row.ProviderTemplateName, row.RequiresConsent, parameters,
            row.IdempotencyKey, row.Status, row.AttemptCount);
    }

    public async Task RecordAttemptAsync(Guid tenantId, Guid notificationId, NotificationDeliveryStatus status, string? providerMessageId, string? safeErrorCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var notification = await context.Notifications.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == notificationId, cancellationToken)
            ?? throw new NotFoundException("Notification was not found.");
        var nextAttempt = notification.AttemptCount + 1;
        notification.RecordAttempt(status, providerMessageId, safeErrorCode, now);
        await context.NotificationAttempts.AddAsync(new NotificationAttemptRecord(Guid.NewGuid(), tenantId, notification.EstablishmentId,
            notificationId, nextAttempt, status, providerMessageId, safeErrorCode, now), cancellationToken);
    }

    internal static string NormalizeDestination(NotificationChannel channel, string destination) => channel switch
    {
        NotificationChannel.Email => destination.Trim().ToUpperInvariant(),
        NotificationChannel.WhatsApp => new string(destination.Where(char.IsDigit).ToArray()),
        _ => destination.Trim().ToUpperInvariant()
    };

    internal static NotificationTemplateView Map(NotificationTemplateRecord x) => new(x.Id, x.Purpose, x.Channel, x.Language, x.Subject, x.Body,
        x.ProviderTemplateName, x.RequiresConsent, x.IsActive, x.UpdatedAtUtc);
}
