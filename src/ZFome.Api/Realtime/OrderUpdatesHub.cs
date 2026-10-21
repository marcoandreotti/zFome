using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Identity;

namespace ZFome.Api.Realtime;

[Authorize(Policy = AdministrativePolicies.OrderRead)]
public sealed class OrderUpdatesHub(
    IEstablishmentAccessGateway accessGateway,
    IPlatformScopeGateway platformScopeGateway,
    OrderUpdateSubscriptions subscriptions,
    OrderRealtimeTelemetry telemetry,
    ILogger<OrderUpdatesHub> logger) : Hub
{
    public const string ClientMethod = "OrderUpdated";

    public override async Task OnConnectedAsync()
    {
        telemetry.ConnectionOpened();
        logger.LogInformation("Order realtime connection {ConnectionId} opened.", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public async Task SubscribeAsync(Guid establishmentId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue("sub"), out var userId) ||
            userId == Guid.Empty ||
            establishmentId == Guid.Empty)
            throw new ForbiddenException("A valid authenticated establishment context is required.");
        var isPlatformUser = Context.User?.HasClaim("platform_user", "true") == true;
        Guid tenantId;
        if (isPlatformUser)
        {
            tenantId = await platformScopeGateway.FindTenantIdAsync(
                establishmentId,
                Context.ConnectionAborted) ??
                throw new ForbiddenException("An authorized establishment context is required.");
        }
        else
        {
            if (!Guid.TryParse(Context.User?.FindFirstValue("tenant_id"), out tenantId) ||
                tenantId == Guid.Empty ||
                !await accessGateway.HasActiveAccessAsync(
                    tenantId,
                    userId,
                    establishmentId,
                    Context.ConnectionAborted))
                throw new ForbiddenException("An authorized establishment context is required.");
        }
        var subscription = new OrderUpdateSubscription(
            Context.ConnectionId,
            tenantId,
            establishmentId,
            userId,
            isPlatformUser);
        subscriptions.Set(subscription);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            OrderUpdateGroups.For(tenantId, establishmentId),
            Context.ConnectionAborted);
        telemetry.SubscriptionAccepted();
        logger.LogInformation(
            "Order realtime connection {ConnectionId} subscribed to establishment {EstablishmentId}.",
            Context.ConnectionId,
            establishmentId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        subscriptions.Remove(Context.ConnectionId);
        telemetry.ConnectionClosed();
        if (exception is null)
            logger.LogInformation("Order realtime connection {ConnectionId} closed.", Context.ConnectionId);
        else
        {
            telemetry.Failure();
            logger.LogWarning(exception, "Order realtime connection {ConnectionId} failed.", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
