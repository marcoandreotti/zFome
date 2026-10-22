using System.Text.Json;
using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using ZFome.Api.Realtime;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Contracts.Realtime;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;

namespace ZFome.Integration.Tests;

public sealed class RealtimeOrderNotificationTests
{
    [Fact]
    public void Versioned_contract_serializes_without_tenant_or_domain_entities()
    {
        var message = new OrderUpdatedMessageV1(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "StatusChanged",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(
            message,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"version\":1", json);
        Assert.Contains("\"orderId\":", json);
        Assert.Contains("\"changeType\":\"StatusChanged\"", json);
        Assert.Contains("\"establishmentId\":", json);
        Assert.DoesNotContain("tenantId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "ZFome.Domain",
            typeof(OrderUpdatedMessageV1).Assembly.GetReferencedAssemblies()
                .Select(name => name.Name));
    }

    [Fact]
    public async Task Publisher_targets_only_the_tenant_unit_group_and_removes_revoked_subscriptions()
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var unitA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var unitB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
        var userA = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
        var revokedUser = Guid.Parse("aaaaaaaa-4444-4444-4444-444444444444");
        var userB = Guid.Parse("bbbbbbbb-5555-5555-5555-555555555555");
        var subscriptions = new OrderUpdateSubscriptions();
        subscriptions.Set(new("connection-a", tenantA, unitA, userA, false));
        subscriptions.Set(new("connection-revoked", tenantA, unitA, revokedUser, false));
        subscriptions.Set(new("connection-b", tenantB, unitB, userB, false));
        var access = new AccessGateway((tenantA, userA, unitA), (tenantB, userB, unitB));
        var clients = new RecordingHubClients();
        var groups = new RecordingGroupManager();
        using var telemetry = new OrderRealtimeTelemetry();
        var publisher = new SignalROrderUpdatePublisher(
            new HubContext(clients, groups),
            subscriptions,
            access,
            new PlatformScopeGateway(),
            telemetry,
            NullLogger<SignalROrderUpdatePublisher>.Instance);

        await publisher.PublishAsync(
            tenantA,
            new OrderUpdateSignal(unitA, Guid.NewGuid(), OrderUpdateKind.StatusChanged, DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.Equal(OrderUpdateGroups.For(tenantA, unitA), Assert.Single(clients.TargetedGroups));
        Assert.DoesNotContain(OrderUpdateGroups.For(tenantB, unitB), clients.TargetedGroups);
        Assert.Contains(("connection-revoked", OrderUpdateGroups.For(tenantA, unitA)), groups.Removed);
        Assert.DoesNotContain(subscriptions.Get(tenantA, unitA), value => value.UserId == revokedUser);
        var message = Assert.IsType<OrderUpdatedMessageV1>(Assert.Single(clients.Proxy.Messages).Arguments[0]);
        Assert.Equal(unitA, message.EstablishmentId);
    }

    [Fact]
    public async Task Hub_accepts_only_server_authorized_establishment_subscriptions()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var allowedUnit = Guid.NewGuid();
        var deniedUnit = Guid.NewGuid();
        var access = new AccessGateway((tenant, user, allowedUnit));
        var subscriptions = new OrderUpdateSubscriptions();
        var groups = new RecordingGroupManager();
        using var telemetry = new OrderRealtimeTelemetry();
        var allowedHub = CreateHub("allowed", tenant, user, access, subscriptions, groups, telemetry);

        await allowedHub.SubscribeAsync(allowedUnit);

        var subscription = Assert.Single(subscriptions.Get(tenant, allowedUnit));
        Assert.Equal(user, subscription.UserId);
        Assert.Contains(("allowed", OrderUpdateGroups.For(tenant, allowedUnit)), groups.Added);

        var deniedHub = CreateHub("denied", tenant, user, access, subscriptions, groups, telemetry);
        await Assert.ThrowsAsync<ForbiddenException>(() => deniedHub.SubscribeAsync(deniedUnit));
        Assert.Empty(subscriptions.Get(tenant, deniedUnit));
    }

    [Fact]
    public async Task Two_live_connections_receive_only_updates_from_their_authorized_unit()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var unitA = Guid.NewGuid();
        var unitB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var access = new AccessGateway((tenantA, userA, unitA), (tenantB, userB, unitB));
        await using var factory = new RealtimeFactory(access);
        await using var connectionA = CreateConnection(factory, tenantA, userA);
        await using var connectionB = CreateConnection(factory, tenantB, userB);
        var receivedA = new TaskCompletionSource<OrderUpdatedMessageV1>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedB = new List<OrderUpdatedMessageV1>();
        connectionA.On<OrderUpdatedMessageV1>(
            OrderUpdatesHub.ClientMethod,
            message => receivedA.TrySetResult(message));
        connectionB.On<OrderUpdatedMessageV1>(
            OrderUpdatesHub.ClientMethod,
            message => receivedB.Add(message));

        await connectionA.StartAsync();
        await connectionB.StartAsync();
        await connectionA.InvokeAsync("SubscribeAsync", unitA);
        await connectionB.InvokeAsync("SubscribeAsync", unitB);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOrderUpdatePublisher>()
                .PublishAsync(
                    tenantA,
                    new OrderUpdateSignal(unitA, Guid.NewGuid(), OrderUpdateKind.Confirmed, DateTimeOffset.UtcNow),
                    CancellationToken.None);
        }

        var message = await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200);
        Assert.Equal(unitA, message.EstablishmentId);
        Assert.Empty(receivedB);
    }

    private static HubConnection CreateConnection(
        WebApplicationFactory<Program> factory,
        Guid tenantId,
        Guid userId)
    {
        var cookies = new CookieContainer();
        cookies.Add(factory.Server.BaseAddress, new Cookie("zf_csrf", "test-csrf"));
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/hubs/order-updates"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Cookies = cookies;
                    options.Headers["Cookie"] = "zf_csrf=test-csrf";
                    options.Headers["X-CSRF-Token"] = "test-csrf";
                    options.Headers["X-Test-Tenant"] = tenantId.ToString();
                    options.Headers["X-Test-User"] = userId.ToString();
                })
            .Build();
    }

    private static OrderUpdatesHub CreateHub(
        string connectionId,
        Guid tenantId,
        Guid userId,
        IEstablishmentAccessGateway access,
        OrderUpdateSubscriptions subscriptions,
        IGroupManager groups,
        OrderRealtimeTelemetry telemetry)
    {
        var hub = new OrderUpdatesHub(
            access,
            new PlatformScopeGateway(),
            subscriptions,
            telemetry,
            NullLogger<OrderUpdatesHub>.Instance)
        {
            Context = new CallerContext(connectionId, tenantId, userId),
            Groups = groups
        };
        return hub;
    }

    private sealed class AccessGateway(params (Guid TenantId, Guid UserId, Guid EstablishmentId)[] active)
        : IEstablishmentAccessGateway
    {
        private readonly HashSet<(Guid, Guid, Guid)> values = [.. active];

        public Task<bool> HasActiveAccessAsync(
            Guid tenantId,
            Guid userId,
            Guid establishmentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(values.Contains((tenantId, userId, establishmentId)));
    }

    private sealed class CallerContext(string connectionId, Guid tenantId, Guid userId) : HubCallerContext
    {
        public override string ConnectionId { get; } = connectionId;
        public override string? UserIdentifier => User.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal User { get; } = new(
            new ClaimsIdentity(
                [
                    new("sub", userId.ToString()),
                    new(ClaimTypes.NameIdentifier, userId.ToString()),
                    new("tenant_id", tenantId.ToString())
                ],
                "Test"));
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private sealed class PlatformScopeGateway : IPlatformScopeGateway
    {
        public Task<Guid?> FindTenantIdAsync(Guid establishmentId, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);
    }

    private sealed class RealtimeFactory(AccessGateway access) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });
                services.RemoveAll<IEstablishmentAccessGateway>();
                services.AddSingleton<IEstablishmentAccessGateway>(access);
                services.Configure<HubOptions>(options => options.EnableDetailedErrors = true);
            });
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "RealtimeTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Tenant"], out var tenantId) ||
                !Guid.TryParse(Request.Headers["X-Test-User"], out var userId))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(
                [
                    new("sub", userId.ToString()),
                    new(ClaimTypes.NameIdentifier, userId.ToString()),
                    new("tenant_id", tenantId.ToString()),
                    new("session_id", Guid.NewGuid().ToString()),
                    new(ClaimTypes.Role, Domain.Identity.AdministrativeRole.Manager.ToString())
                ],
                SchemeName);
            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class HubContext(IHubClients clients, IGroupManager groups)
        : IHubContext<OrderUpdatesHub>
    {
        public IHubClients Clients { get; } = clients;
        public IGroupManager Groups { get; } = groups;
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string Group)> Added { get; } = [];
        public List<(string ConnectionId, string Group)> Removed { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHubClients : IHubClients
    {
        public RecordingClientProxy Proxy { get; } = new();
        public List<string> TargetedGroups { get; } = [];
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) { TargetedGroups.Add(groupName); return Proxy; }
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Arguments)> Messages { get; } = [];

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            Messages.Add((method, args));
            return Task.CompletedTask;
        }
    }
}
