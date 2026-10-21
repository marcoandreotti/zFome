using Dapper;
using ZFome.Application.Abstractions.Communications;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Tenancy;

namespace ZFome.Infrastructure.Persistence.Read;

internal sealed class NotificationReadGateway(IReadConnectionFactory connections) : INotificationReadGateway
{
    public async Task<IReadOnlyList<NotificationTemplateView>> ListTemplatesAsync(OperationalScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            select id, purpose, channel, language, subject, body, provider_template_name as ProviderTemplateName,
                   requires_consent as RequiresConsent, is_active as IsActive, updated_at_utc as UpdatedAtUtc
            from communications.notification_template
            where tenant_id = @TenantId and establishment_id = @EstablishmentId
            order by purpose, channel, language;
            """;
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TemplateRow>(new CommandDefinition(sql, scope, cancellationToken: cancellationToken));
        return rows.Select(x => new NotificationTemplateView(x.Id, x.Purpose, Enum.Parse<NotificationChannel>(x.Channel), x.Language,
            x.Subject, x.Body, x.ProviderTemplateName, x.RequiresConsent, x.IsActive, new DateTimeOffset(DateTime.SpecifyKind(x.UpdatedAtUtc, DateTimeKind.Utc)))).ToArray();
    }

    public async Task<IReadOnlyList<NotificationConsentView>> ListConsentsAsync(OperationalScope scope, CancellationToken cancellationToken)
    {
        const string sql = """
            select id, channel, purpose, destination, is_granted as IsGranted, captured_at_utc as CapturedAtUtc, source
            from communications.notification_consent
            where tenant_id = @TenantId and establishment_id = @EstablishmentId
            order by captured_at_utc desc, id desc limit 500;
            """;
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ConsentRow>(new CommandDefinition(sql, scope, cancellationToken: cancellationToken));
        return rows.Select(x => new NotificationConsentView(x.Id, Enum.Parse<NotificationChannel>(x.Channel), x.Purpose, x.Destination,
            x.IsGranted, new DateTimeOffset(DateTime.SpecifyKind(x.CapturedAtUtc, DateTimeKind.Utc)), x.Source)).ToArray();
    }

    public async Task<IReadOnlyList<NotificationHistoryView>> ListHistoryAsync(OperationalScope scope, int page, int pageSize, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, CancellationToken cancellationToken)
    {
        const string sql = """
            select id, channel, purpose, destination, status, provider_message_id as ProviderMessageId,
                   attempt_count as AttemptCount, last_error as LastError, created_at_utc as CreatedAtUtc, updated_at_utc as UpdatedAtUtc
            from communications.notification
            where tenant_id = @TenantId and establishment_id = @EstablishmentId
              and (@FromUtc is null or created_at_utc >= @FromUtc)
              and (@ToUtcExclusive is null or created_at_utc < @ToUtcExclusive)
            order by created_at_utc desc
            offset @Offset limit @PageSize;
            """;
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<HistoryRow>(new CommandDefinition(sql,
            new { scope.TenantId, scope.EstablishmentId, Offset = (page - 1) * pageSize, PageSize = pageSize, FromUtc = fromUtc?.UtcDateTime, ToUtcExclusive = toUtcExclusive?.UtcDateTime }, cancellationToken: cancellationToken));
        return rows.Select(x => new NotificationHistoryView(x.Id, Enum.Parse<NotificationChannel>(x.Channel), x.Purpose, x.Destination,
            Enum.Parse<NotificationDeliveryStatus>(x.Status), x.ProviderMessageId, x.AttemptCount, x.LastError,
            new DateTimeOffset(DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc)), x.UpdatedAtUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(x.UpdatedAtUtc.Value, DateTimeKind.Utc)))).ToArray();
    }

    public async Task<IReadOnlyList<NotificationAttemptView>> ListAttemptsAsync(OperationalScope scope, Guid notificationId, CancellationToken cancellationToken)
    {
        const string sql = """
            select attempt.attempt_number as AttemptNumber, attempt.status as Status,
                   attempt.provider_message_id as ProviderMessageId, attempt.safe_error_code as SafeErrorCode,
                   attempt.created_at_utc as CreatedAtUtc
            from communications.notification_attempt as attempt
            inner join communications.notification as notification
                on notification.tenant_id = attempt.tenant_id
               and notification.establishment_id = attempt.establishment_id
               and notification.id = attempt.notification_id
            where notification.tenant_id = @TenantId
              and notification.establishment_id = @EstablishmentId
              and notification.id = @NotificationId
            order by attempt.attempt_number;
            """;
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AttemptRow>(new CommandDefinition(sql,
            new { scope.TenantId, scope.EstablishmentId, NotificationId = notificationId }, cancellationToken: cancellationToken));
        return rows.Select(x => new NotificationAttemptView(x.AttemptNumber, Enum.Parse<NotificationDeliveryStatus>(x.Status),
            x.ProviderMessageId, x.SafeErrorCode, new DateTimeOffset(DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc)))).ToArray();
    }

    private sealed record TemplateRow(Guid Id, string Purpose, string Channel, string Language, string Subject, string Body,
        string? ProviderTemplateName, bool RequiresConsent, bool IsActive, DateTime UpdatedAtUtc);
    private sealed record ConsentRow(Guid Id, string Channel, string Purpose, string Destination, bool IsGranted, DateTime CapturedAtUtc, string? Source);
    private sealed record HistoryRow(Guid Id, string Channel, string Purpose, string Destination, string Status, string? ProviderMessageId,
        int AttemptCount, string? LastError, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
    private sealed record AttemptRow(int AttemptNumber, string Status, string? ProviderMessageId, string? SafeErrorCode, DateTime CreatedAtUtc);
}
