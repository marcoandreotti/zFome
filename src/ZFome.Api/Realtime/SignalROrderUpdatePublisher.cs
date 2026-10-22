using Microsoft.AspNetCore.SignalR;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Contracts.Realtime;

namespace ZFome.Api.Realtime;

public sealed class SignalROrderUpdatePublisher(
    IHubContext<OrderUpdatesHub> hubContext,
    OrderUpdateSubscriptions subscriptions,
    IEstablishmentAccessGateway accessGateway,
    IPlatformScopeGateway platformScopeGateway,
    OrderRealtimeTelemetry telemetry,
    ILogger<SignalROrderUpdatePublisher> logger) : IOrderUpdatePublisher
{
    public async Task PublishAsync(
        Guid tenantId,
        OrderUpdateSignal signal,
        CancellationToken cancellationToken)
    {
        try
        {
            var group = OrderUpdateGroups.For(tenantId, signal.EstablishmentId);
            foreach (var subscription in subscriptions.Get(tenantId, signal.EstablishmentId))
            {
                var authorized = subscription.IsPlatformUser
                    ? await platformScopeGateway.FindTenantIdAsync(signal.EstablishmentId, cancellationToken) == tenantId
                    : await accessGateway.HasActiveAccessAsync(
                        tenantId,
                        subscription.UserId,
                        signal.EstablishmentId,
                        cancellationToken);
                if (authorized)
                    continue;

                subscriptions.Remove(subscription.ConnectionId);
                await hubContext.Groups.RemoveFromGroupAsync(
                    subscription.ConnectionId,
                    group,
                    cancellationToken);
                telemetry.DeliveryDenied();
                logger.LogWarning(
                    "Denied realtime order delivery to connection {ConnectionId} for establishment {EstablishmentId}.",
                    subscription.ConnectionId,
                    signal.EstablishmentId);
            }

            var message = new OrderUpdatedMessageV1(
                signal.OrderId,
                signal.ChangeType.ToString(),
                signal.EstablishmentId,
                signal.OccurredAt);
            await hubContext.Clients.Group(group)
                .SendAsync(OrderUpdatesHub.ClientMethod, message, cancellationToken);
            telemetry.EventPublished();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            telemetry.Failure();
            logger.LogWarning(
                "Realtime order publication was cancelled after persistence for order {OrderId}.",
                signal.OrderId);
        }
        catch (Exception exception)
        {
            telemetry.Failure();
            logger.LogError(
                exception,
                "Realtime order publication failed after persistence for order {OrderId}.",
                signal.OrderId);
        }
    }
}
