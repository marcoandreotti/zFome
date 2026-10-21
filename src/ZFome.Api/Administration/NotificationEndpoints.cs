using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Communications;
using ZFome.Application.Identity;
using ZFome.Application.Abstractions.Communications;

namespace ZFome.Api.Administration;

internal static class NotificationEndpoints
{
    public static void MapNotifications(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/communications").RequireAuthorization(AdministrativePolicies.Management)
            .WithTags("Administration - Communications");
        group.MapGet("/templates", async (Guid establishmentId, IQueryDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var templates = await dispatcher.DispatchAsync<ListNotificationTemplatesQuery, IReadOnlyList<NotificationTemplateView>>(
                new(establishmentId), cancellationToken);
            return Results.Ok(templates.Select(Map).ToArray());
        });
        group.MapPut("/templates", async (Guid establishmentId, NotificationTemplateRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<NotificationChannel>(request.Channel, true, out var channel))
                throw new FluentValidation.ValidationException("O canal deve ser E-mail ou WhatsApp.");
            var id = await dispatcher.DispatchAsync<UpsertNotificationTemplateCommand, Guid>(new(establishmentId, request.Id,
                request.Purpose, channel, request.Language, request.Subject ?? string.Empty, request.Body, request.ProviderTemplateName,
                request.RequiresConsent, request.IsActive), cancellationToken);
            return Results.Ok(new { id });
        });
        group.MapGet("/consents", async (Guid establishmentId, IQueryDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var consents = await dispatcher.DispatchAsync<ListNotificationConsentsQuery, IReadOnlyList<NotificationConsentView>>(
                new(establishmentId), cancellationToken);
            return Results.Ok(consents.Select(Map).ToArray());
        });
        group.MapPut("/consents", async (Guid establishmentId, NotificationConsentRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<NotificationChannel>(request.Channel, true, out var channel))
                throw new FluentValidation.ValidationException("O canal deve ser E-mail ou WhatsApp.");
            await dispatcher.DispatchAsync(new SetNotificationConsentCommand(establishmentId, channel, request.Purpose,
                request.Destination, request.IsGranted, request.Source), cancellationToken);
            return Results.NoContent();
        });
        group.MapPost("/notifications", async (Guid establishmentId, NotificationCreateRequest request, ICommandDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<NotificationChannel>(request.Channel, true, out var channel))
                throw new FluentValidation.ValidationException("O canal deve ser E-mail ou WhatsApp.");
            var id = await dispatcher.DispatchAsync<RequestNotificationCommand, Guid>(new(establishmentId, request.TemplateId,
                channel, request.Destination, request.Parameters, request.IdempotencyKey), cancellationToken);
            return Results.Accepted(value: new { id });
        });
        group.MapGet("/notifications", async (Guid establishmentId, int? page, int? pageSize, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, IQueryDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var items = await dispatcher.DispatchAsync<ListNotificationHistoryQuery, IReadOnlyList<NotificationHistoryView>>(
                new(establishmentId, page ?? 1, pageSize ?? 20, fromUtc, toUtcExclusive), cancellationToken);
            return Results.Ok(items.Select(Map).ToArray());
        });
        group.MapGet("/notifications/{notificationId:guid}/attempts", async (Guid establishmentId, Guid notificationId, IQueryDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var attempts = await dispatcher.DispatchAsync<ListNotificationAttemptsQuery, IReadOnlyList<NotificationAttemptView>>(
                new(establishmentId, notificationId), cancellationToken);
            return Results.Ok(attempts.Select(x => new NotificationAttemptResponse(x.AttemptNumber, x.Status.ToString(),
                x.ProviderMessageId, x.SafeErrorCode, x.CreatedAtUtc)).ToArray());
        });
    }

    private static NotificationTemplateResponse Map(NotificationTemplateView x) => new(x.Id, x.Purpose, x.Channel.ToString(), x.Language,
        x.Subject, x.Body, x.ProviderTemplateName, x.RequiresConsent, x.IsActive, x.UpdatedAtUtc);
    private static NotificationConsentResponse Map(NotificationConsentView x) => new(x.Id, x.Channel.ToString(), x.Purpose,
        x.Destination, x.IsGranted, x.CapturedAtUtc, x.Source);
    private static NotificationHistoryResponse Map(NotificationHistoryView x) => new(x.Id, x.Channel.ToString(), x.Purpose, x.Destination,
        x.Status.ToString(), x.ProviderMessageId, x.AttemptCount, x.LastError, x.CreatedAtUtc, x.UpdatedAtUtc);
}

internal sealed record NotificationTemplateRequest(Guid? Id, string Purpose, string Channel, string Language, string? Subject,
    string Body, string? ProviderTemplateName, bool RequiresConsent, bool IsActive);
internal sealed record NotificationTemplateResponse(Guid Id, string Purpose, string Channel, string Language, string Subject,
    string Body, string? ProviderTemplateName, bool RequiresConsent, bool IsActive, DateTimeOffset UpdatedAtUtc);
internal sealed record NotificationConsentRequest(string Channel, string Purpose, string Destination, bool IsGranted, string? Source);
internal sealed record NotificationConsentResponse(Guid Id, string Channel, string Purpose, string Destination, bool IsGranted,
    DateTimeOffset CapturedAtUtc, string? Source);
internal sealed record NotificationCreateRequest(Guid TemplateId, string Channel, string Destination, IReadOnlyDictionary<string, string> Parameters,
    string IdempotencyKey);
internal sealed record NotificationHistoryResponse(Guid Id, string Channel, string Purpose, string Destination, string Status,
    string? ProviderMessageId, int AttemptCount, string? LastError, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
internal sealed record NotificationAttemptResponse(int AttemptNumber, string Status, string? ProviderMessageId, string? SafeErrorCode,
    DateTimeOffset CreatedAtUtc);
