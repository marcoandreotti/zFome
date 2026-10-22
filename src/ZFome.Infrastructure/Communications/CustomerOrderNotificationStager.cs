using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZFome.Application.Abstractions.Communications;
using ZFome.Domain.Ordering;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Infrastructure.Communications;

internal sealed class CustomerOrderNotificationStager(
    ZFomeDbContext context,
    NotificationRequestOutboxStager outbox,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<CustomerOrderNotificationStager> logger) : ICustomerOrderNotificationStager
{
    private static readonly Regex PlaceholderPattern = new("\\{\\{([a-zA-Z0-9_.-]{1,50})\\}\\}", RegexOptions.Compiled);
    public async Task StageAsync(Order order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var purpose = GetPurpose(order.Status);
        if (purpose is null || order.Number is null || string.IsNullOrWhiteSpace(order.PublicReference)
            || order.CustomerId is not { } customerId)
            return;

        var isPublicOrder = await context.PublicOrderRequests.AsNoTracking().AnyAsync(
            request => request.TenantId == order.TenantId && request.EstablishmentId == order.EstablishmentId
                && request.OrderId == order.Id, cancellationToken);
        if (!isPublicOrder)
            return;

        var customer = await context.Customers.AsNoTracking()
            .Where(customer => customer.TenantId == order.TenantId && customer.EstablishmentId == order.EstablishmentId
                && customer.Id == customerId)
            .Select(customer => new { customer.Email, customer.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(customer?.Email))
            return;

        var template = await context.NotificationTemplates.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.TenantId == order.TenantId && candidate.EstablishmentId == order.EstablishmentId
                && candidate.Purpose == purpose && candidate.Channel == NotificationChannel.Email
                && candidate.Language == "pt_BR" && candidate.IsActive,
            cancellationToken);
        if (template is null)
            return;

        var requiredParameters = PlaceholderPattern.Matches(template.Subject + "\n" + template.Body)
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        if (!requiredParameters.IsSubsetOf(CustomerOrderNotificationParameters.All))
        {
            logger.LogWarning("Customer order notification template has unsupported placeholders for purpose {Purpose}.", purpose);
            return;
        }

        var statusLabel = StatusLabel(order.Status);
        var trackingUrl = BuildTrackingUrl(order.PublicReference);
        var availableParameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = customer.Name,
            ["orderNumber"] = order.Number.Value.ToString(CultureInfo.InvariantCulture),
            ["status"] = order.Status.ToString(),
            ["statusLabel"] = statusLabel,
            ["trackingReference"] = order.PublicReference,
            ["trackingUrl"] = trackingUrl,
            ["total"] = order.Total.Amount.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR")),
            ["serviceType"] = ServiceLabel(order.ServiceType)
        };
        var parameters = requiredParameters.ToDictionary(key => key, key => availableParameters[key], StringComparer.Ordinal);
        var idempotencyKey = $"customer-order:{order.Id:N}:{purpose}";

        var existing = await context.Notifications.AsNoTracking().AnyAsync(
            notification => notification.TenantId == order.TenantId && notification.EstablishmentId == order.EstablishmentId
                && notification.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing)
            return;

        var now = timeProvider.GetUtcNow();
        var templateView = new NotificationTemplateView(template.Id, template.Purpose, template.Channel, template.Language,
            template.Subject, template.Body, template.ProviderTemplateName, template.RequiresConsent, template.IsActive, template.UpdatedAtUtc);
        var request = new NotificationRequestDraft(order.EstablishmentId, template.Id, customer.Email, parameters, idempotencyKey);
        var notificationId = Guid.NewGuid();
        var parametersJson = JsonSerializer.Serialize(parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        context.Notifications.Add(new NotificationRecord(notificationId, order.TenantId, order.EstablishmentId, templateView,
            request, NotificationWriteRepository.NormalizeDestination(NotificationChannel.Email, customer.Email), parametersJson, now));
        outbox.Stage(order.TenantId, notificationId, $"notification:{notificationId:N}");
    }

    private string BuildTrackingUrl(string reference)
    {
        var baseUrl = configuration["CustomerOrderNotifications:PublicBaseUrl"]
            ?? configuration["Cors:AllowedOrigins:0"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin))
            return $"/order/track/{Uri.EscapeDataString(reference)}";
        return new Uri(origin, $"/order/track/{Uri.EscapeDataString(reference)}").ToString();
    }

    private static string? GetPurpose(OrderStatus status) => status switch
    {
        OrderStatus.Confirmed => CustomerOrderNotificationPurposes.Confirmed,
        OrderStatus.Preparing => CustomerOrderNotificationPurposes.Preparing,
        OrderStatus.Ready => CustomerOrderNotificationPurposes.Ready,
        OrderStatus.OutForDelivery => CustomerOrderNotificationPurposes.OutForDelivery,
        OrderStatus.Completed => CustomerOrderNotificationPurposes.Completed,
        OrderStatus.Cancelled => CustomerOrderNotificationPurposes.Cancelled,
        OrderStatus.Rejected => CustomerOrderNotificationPurposes.Rejected,
        _ => null
    };

    private static string StatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Confirmed => "confirmado",
        OrderStatus.Preparing => "em preparo",
        OrderStatus.Ready => "pronto",
        OrderStatus.OutForDelivery => "saiu para entrega",
        OrderStatus.Completed => "concluído",
        OrderStatus.Cancelled => "cancelado",
        OrderStatus.Rejected => "recusado",
        _ => status.ToString()
    };

    private static string ServiceLabel(OrderServiceType serviceType) => serviceType switch
    {
        OrderServiceType.Table => "consumo no local",
        OrderServiceType.Pickup => "retirada",
        OrderServiceType.Delivery => "entrega",
        _ => serviceType.ToString()
    };
}
