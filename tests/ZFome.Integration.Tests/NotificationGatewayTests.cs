using Dapper;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZFome.Application.Abstractions.Communications;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Tenancy;
using ZFome.Domain.Tenancy;
using ZFome.Domain.Customers;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;
using ZFome.Infrastructure;
using ZFome.Infrastructure.Communications;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalMailpitFactAttribute : FactAttribute
{
    public LocalMailpitFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZFOME_MAILPIT_BASE_URL")))
            Skip = "Set ZFOME_MAILPIT_BASE_URL to run the local Mailpit end-to-end smoke test.";
    }
}

public sealed class NotificationGatewayTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Theory]
    [InlineData(NotificationChannel.Email, "customer@example.test")]
    [InlineData(NotificationChannel.WhatsApp, "+15551234567")]
    public async Task Outbox_delivers_each_enabled_channel_and_records_attempt(NotificationChannel channel, string destination)
    {
        var sender = new FakeNotificationSender(channel, new NotificationProviderResult(NotificationProviderOutcome.Accepted, "provider-123", null));
        var services = CreateServices(sender);
        await using var provider = services.BuildServiceProvider();
        var (scope, notificationId) = await CreateRequestAsync(provider, channel, destination, requiresConsent: false);

        var worker = provider.GetServices<IHostedService>().Single(item => item.GetType().Name == "OutboxProcessingWorker");
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForStatusAsync(provider, scope, notificationId, NotificationDeliveryStatus.AcceptedByProvider);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.Equal(1, sender.Calls);
        await using var readScope = provider.CreateAsyncScope();
        var reads = readScope.ServiceProvider.GetRequiredService<INotificationReadGateway>();
        var attempts = await reads.ListAttemptsAsync(scope, notificationId, CancellationToken.None);
        var attempt = Assert.Single(attempts);
        Assert.Equal(NotificationDeliveryStatus.AcceptedByProvider, attempt.Status);
        Assert.Equal("provider-123", attempt.ProviderMessageId);
    }

    [Fact]
    public async Task Missing_required_consent_blocks_delivery_without_calling_provider()
    {
        var sender = new FakeNotificationSender(NotificationChannel.Email, new NotificationProviderResult(NotificationProviderOutcome.Accepted, "unexpected", null));
        var services = CreateServices(sender);
        await using var provider = services.BuildServiceProvider();
        var (scope, notificationId) = await CreateRequestAsync(provider, NotificationChannel.Email, "customer@example.test", requiresConsent: true);
        await using (var consentScope = provider.CreateAsyncScope())
        {
            var repository = consentScope.ServiceProvider.GetRequiredService<INotificationWriteRepository>();
            var now = DateTimeOffset.UtcNow;
            await repository.SetConsentAsync(scope,
                new(NotificationChannel.Email, "order.update", "customer@example.test", true, now, "verified checkout opt-in"),
                now, CancellationToken.None);
            await repository.SetConsentAsync(scope,
                new(NotificationChannel.Email, "order.update", "customer@example.test", false, now.AddSeconds(1), "customer opt-out"),
                now.AddSeconds(1), CancellationToken.None);
        }

        var worker = provider.GetServices<IHostedService>().Single(item => item.GetType().Name == "OutboxProcessingWorker");
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForStatusAsync(provider, scope, notificationId, NotificationDeliveryStatus.BlockedByConsent);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.Equal(0, sender.Calls);
        await using var readScope = provider.CreateAsyncScope();
        var reads = readScope.ServiceProvider.GetRequiredService<INotificationReadGateway>();
        var attempt = Assert.Single(await reads.ListAttemptsAsync(scope, notificationId, CancellationToken.None));
        Assert.Equal("consent_required", attempt.SafeErrorCode);
        var consentHistory = await reads.ListConsentsAsync(scope, CancellationToken.None);
        Assert.Equal(new[] { false, true }, consentHistory.Select(entry => entry.IsGranted));
    }

    [Fact]
    public async Task Transient_provider_failure_schedules_retry_and_keeps_each_attempt()
    {
        var sender = new FakeNotificationSender(NotificationChannel.Email,
            new NotificationProviderResult(NotificationProviderOutcome.RetryableFailure, null, "smtp_421"),
            new NotificationProviderResult(NotificationProviderOutcome.Accepted, "provider-after-retry", null));
        var services = CreateServices(sender, retryDelaySeconds: 1);
        await using var provider = services.BuildServiceProvider();
        var (scope, notificationId) = await CreateRequestAsync(provider, NotificationChannel.Email, "customer@example.test", requiresConsent: false);
        var worker = provider.GetServices<IHostedService>().Single(item => item.GetType().Name == "OutboxProcessingWorker");
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForStatusAsync(provider, scope, notificationId, NotificationDeliveryStatus.AcceptedByProvider);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.Equal(2, sender.Calls);
        await using var readScope = provider.CreateAsyncScope();
        var reads = readScope.ServiceProvider.GetRequiredService<INotificationReadGateway>();
        var attempts = await reads.ListAttemptsAsync(scope, notificationId, CancellationToken.None);
        Assert.Equal(new[] { NotificationDeliveryStatus.RetryScheduled, NotificationDeliveryStatus.AcceptedByProvider },
            attempts.Select(attempt => attempt.Status));
    }

    [Fact]
    public async Task Templates_and_history_are_scoped_to_the_authorized_tenant_and_establishment()
    {
        var services = CreateServices(new FakeNotificationSender(NotificationChannel.Email,
            new NotificationProviderResult(NotificationProviderOutcome.Accepted, "provider", null)));
        await using var provider = services.BuildServiceProvider();
        await using var child = provider.CreateAsyncScope();
        var context = child.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        await context.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var tenantA = Tenant.Create("Communications A", now);
        var tenantB = Tenant.Create("Communications B", now);
        var establishmentA = Establishment.Create(tenantA.Id, "A", new Slug("communications-a-" + Guid.NewGuid().ToString("N")), now);
        var establishmentB = Establishment.Create(tenantB.Id, "B", new Slug("communications-b-" + Guid.NewGuid().ToString("N")), now);
        context.AddRange(tenantA, tenantB, establishmentA, establishmentB);
        await context.SaveChangesAsync();
        var scopeA = new OperationalScope(tenantA.Id, Guid.NewGuid(), establishmentA.Id);
        var scopeB = new OperationalScope(tenantB.Id, Guid.NewGuid(), establishmentB.Id);
        var repository = child.ServiceProvider.GetRequiredService<INotificationWriteRepository>();
        var reads = child.ServiceProvider.GetRequiredService<INotificationReadGateway>();
        var templateId = await repository.UpsertTemplateAsync(scopeA,
            new(null, "order.update", NotificationChannel.Email, "pt_BR", "Order", "Order {{id}}", null, false, true), now, CancellationToken.None);
        var template = await repository.FindTemplateAsync(scopeA, templateId, CancellationToken.None);
        Assert.NotNull(template);
        _ = await repository.RequestAsync(scopeA,
            new(establishmentA.Id, templateId, "customer@example.test", new Dictionary<string, string> { ["id"] = "12" }, "tenant-a-request"),
            template!, "{\"id\":\"12\"}", now, CancellationToken.None);

        Assert.Null(await repository.FindTemplateAsync(scopeB, templateId, CancellationToken.None));
        await Assert.ThrowsAsync<ZFome.Application.Exceptions.NotFoundException>(() => repository.UpsertTemplateAsync(scopeB,
            new(templateId, "order.update", NotificationChannel.Email, "pt_BR", "Changed", "Changed", null, false, true),
            now, CancellationToken.None));
        Assert.Empty(await reads.ListTemplatesAsync(scopeB, CancellationToken.None));
        Assert.Empty(await reads.ListHistoryAsync(scopeB, 1, 10, null, null, CancellationToken.None));
        Assert.Single(await reads.ListTemplatesAsync(scopeA, CancellationToken.None));
        Assert.Single(await reads.ListHistoryAsync(scopeA, 1, 10, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Customer_order_notifications_use_public_data_and_stage_status_events_once()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = database.GetConnectionString(),
            ["CustomerOrderNotifications:PublicBaseUrl"] = "https://orders.example.test"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();
        var fixture = await CreatePublicOrderAsync(provider, "order.confirmed", "Olá {{name}}, pedido {{orderNumber}} {{statusLabel}}: {{trackingUrl}} ({{trackingReference}})", true, true);

        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var order = await context.Orders.SingleAsync(item => item.Id == fixture.OrderId);
            var stager = scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>();
            await stager.StageAsync(order, CancellationToken.None);
            await context.SaveChangesAsync();
            await stager.StageAsync(order, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var connection = verifyContext.Database.GetDbConnection();
        var notification = await connection.QuerySingleAsync<(string Destination, string Purpose, string IdempotencyKey, string ParametersJson)>(
            "select destination as Destination, purpose as Purpose, idempotency_key as IdempotencyKey, parameters_json::text as ParametersJson from communications.notification where tenant_id = @TenantId and establishment_id = @EstablishmentId",
            new { fixture.TenantId, fixture.EstablishmentId });
        Assert.Equal("customer@example.test", notification.Destination);
        Assert.Equal("order.confirmed", notification.Purpose);
        Assert.Equal($"customer-order:{fixture.OrderId:N}:order.confirmed", notification.IdempotencyKey);
        Assert.Contains("https://orders.example.test/order/track/", notification.ParametersJson);
        Assert.Contains("trackingReference", notification.ParametersJson);
        Assert.DoesNotContain(fixture.OrderId.ToString(), notification.ParametersJson);
        Assert.DoesNotContain(fixture.TenantId.ToString(), notification.ParametersJson);
        Assert.Equal(1, await connection.QuerySingleAsync<int>(
            "select count(*) from integration.outbox_message where tenant_id = @TenantId", new { fixture.TenantId }));

        var orderModel = await verifyContext.Orders.SingleAsync(item => item.Id == fixture.OrderId);
        var repository = verifyScope.ServiceProvider.GetRequiredService<INotificationWriteRepository>();
        await repository.UpsertTemplateAsync(new OperationalScope(fixture.TenantId, Guid.NewGuid(), fixture.EstablishmentId),
            new(null, "order.preparing", NotificationChannel.Email, "pt_BR", "Pedido em preparo", "Pedido {{orderNumber}}: {{statusLabel}}", null, false, true),
            DateTimeOffset.UtcNow, CancellationToken.None);
        orderModel.StartPreparation(DateTimeOffset.UtcNow.AddMinutes(1), Guid.NewGuid());
        await verifyScope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>().StageAsync(orderModel, CancellationToken.None);
        await verifyContext.SaveChangesAsync();
        Assert.Equal(OrderStatus.Preparing, orderModel.Status);
        Assert.Equal(2, await connection.QuerySingleAsync<int>(
            "select count(*) from communications.notification where tenant_id = @TenantId", new { fixture.TenantId }));
    }

    [LocalMailpitFact]
    public async Task Customer_order_confirmation_and_status_email_are_visible_in_local_mailpit()
    {
        var mailpitUrl = Environment.GetEnvironmentVariable("ZFOME_MAILPIT_BASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(mailpitUrl));

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = database.GetConnectionString(),
            ["CustomerOrderNotifications:PublicBaseUrl"] = "http://localhost:9000",
            ["NotificationSmtp:Host"] = "localhost",
            ["NotificationSmtp:Port"] = "1025",
            ["NotificationSmtp:FromAddress"] = "no-reply@zfome.test",
            ["NotificationSmtp:EnableSsl"] = "false",
            ["NotificationSmtp:TimeoutSeconds"] = "15",
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Outbox:CleanupIntervalHours"] = "24",
            ["NotificationDelivery:MaximumAttempts"] = "2",
            ["NotificationDelivery:InitialRetryDelaySeconds"] = "1",
            ["NotificationDelivery:MaximumRetryDelaySeconds"] = "2"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        services.RemoveAll<INotificationChannelSender>();
        services.AddSingleton<INotificationChannelSender>(new SmtpNotificationSender(Microsoft.Extensions.Options.Options.Create(
            new NotificationSmtpOptions { Host = "localhost", Port = 1025, FromAddress = "no-reply@zfome.test", EnableSsl = false, TimeoutSeconds = 15 })));
        await using var provider = services.BuildServiceProvider();
        var fixture = await CreatePublicOrderAsync(provider, "order.confirmed", "Pedido {{orderNumber}}", true, true);
        var scopeModel = new OperationalScope(fixture.TenantId, Guid.NewGuid(), fixture.EstablishmentId);
        Guid confirmationNotificationId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<INotificationWriteRepository>();
            await repository.UpsertTemplateAsync(scopeModel,
                new(null, "order.confirmed", NotificationChannel.Email, "pt_BR", "Pedido {{orderNumber}}: {{statusLabel}}",
                    "Olá {{name}}, pedido {{orderNumber}} está {{statusLabel}}. Acompanhe em {{trackingUrl}}.", null, false, true),
                DateTimeOffset.UtcNow, CancellationToken.None);
            var order = await context.Orders.SingleAsync(item => item.Id == fixture.OrderId);
            await scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>().StageAsync(order, CancellationToken.None);
            await context.SaveChangesAsync();
            confirmationNotificationId = await context.Database.GetDbConnection().QuerySingleAsync<Guid>(
                "select id from communications.notification where tenant_id = @TenantId and purpose = 'order.confirmed'", new { fixture.TenantId });
        }

        var worker = provider.GetServices<IHostedService>().Single(item => item.GetType().Name == "OutboxProcessingWorker");
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForStatusAsync(provider, scopeModel, confirmationNotificationId, NotificationDeliveryStatus.AcceptedByProvider);
            await WaitForMailpitSubjectAsync(mailpitUrl, $"Pedido {fixture.OrderNumber}: confirmado", "customer@example.test");

            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<INotificationWriteRepository>();
            await repository.UpsertTemplateAsync(scopeModel,
                new(null, "order.preparing", NotificationChannel.Email, "pt_BR", "Pedido {{orderNumber}}: {{statusLabel}}",
                    "Olá {{name}}, pedido {{orderNumber}} está {{statusLabel}}. Acompanhe em {{trackingUrl}}.", null, false, true),
                DateTimeOffset.UtcNow, CancellationToken.None);
            var order = await context.Orders.SingleAsync(item => item.Id == fixture.OrderId);
            order.StartPreparation(DateTimeOffset.UtcNow.AddMinutes(1), Guid.NewGuid());
            await scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>().StageAsync(order, CancellationToken.None);
            await context.SaveChangesAsync();
            var preparingNotificationId = await context.Database.GetDbConnection().QuerySingleAsync<Guid>(
                "select id from communications.notification where tenant_id = @TenantId and purpose = 'order.preparing'", new { fixture.TenantId });
            await WaitForStatusAsync(provider, scopeModel, preparingNotificationId, NotificationDeliveryStatus.AcceptedByProvider);
            await WaitForMailpitSubjectAsync(mailpitUrl, $"Pedido {fixture.OrderNumber}: em preparo", "customer@example.test");
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        Assert.Equal(OrderStatus.Preparing, (await verifyContext.Orders.SingleAsync(item => item.Id == fixture.OrderId)).Status);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Customer_order_notification_is_skipped_when_email_or_matching_template_is_missing(bool hasEmail, bool hasTemplate)
    {
        var services = CreateServices(new FakeNotificationSender(NotificationChannel.Email,
            new NotificationProviderResult(NotificationProviderOutcome.Accepted, "unused", null)));
        await using var provider = services.BuildServiceProvider();
        var fixture = await CreatePublicOrderAsync(provider, "order.confirmed", "Pedido {{orderNumber}}", hasEmail, hasTemplate);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var order = await context.Orders.SingleAsync(item => item.Id == fixture.OrderId);
        await scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>().StageAsync(order, CancellationToken.None);
        await context.SaveChangesAsync();
        Assert.Equal(0, await context.Database.GetDbConnection().QuerySingleAsync<int>(
            "select count(*) from communications.notification where tenant_id = @TenantId", new { fixture.TenantId }));
    }

    [Fact]
    public async Task Public_order_status_and_notification_request_roll_back_together()
    {
        var services = CreateServices(new FakeNotificationSender(NotificationChannel.Email,
            new NotificationProviderResult(NotificationProviderOutcome.Accepted, "unused", null)));
        await using var provider = services.BuildServiceProvider();
        var fixture = await CreatePublicOrderAsync(provider, "order.preparing", "Pedido {{orderNumber}}: {{statusLabel}}", true, true);
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var order = await context.Orders.SingleAsync(item => item.Id == fixture.OrderId);
            order.StartPreparation(DateTimeOffset.UtcNow.AddMinutes(1), Guid.NewGuid());
            await scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>().StageAsync(order, CancellationToken.None);
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var connection = verifyContext.Database.GetDbConnection();
        Assert.Equal(OrderStatus.Confirmed, (await verifyContext.Orders.SingleAsync(item => item.Id == fixture.OrderId)).Status);
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            "select count(*) from communications.notification where tenant_id = @TenantId", new { fixture.TenantId }));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            "select count(*) from integration.outbox_message where tenant_id = @TenantId and message_type = 'communications.notification.requested'", new { fixture.TenantId }));
    }

    [Fact]
    public async Task Customer_order_delivery_retry_does_not_change_order_state()
    {
        var sender = new FakeNotificationSender(NotificationChannel.Email,
            new NotificationProviderResult(NotificationProviderOutcome.RetryableFailure, null, "smtp_421"),
            new NotificationProviderResult(NotificationProviderOutcome.Accepted, "accepted-after-retry", null));
        var services = CreateServices(sender, retryDelaySeconds: 1);
        await using var provider = services.BuildServiceProvider();
        var fixture = await CreatePublicOrderAsync(provider, "order.confirmed", "Pedido {{orderNumber}}: {{trackingUrl}}", true, true);
        Guid notificationId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var order = await context.Orders.SingleAsync(item => item.Id == fixture.OrderId);
            await scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>().StageAsync(order, CancellationToken.None);
            await context.SaveChangesAsync();
            notificationId = await context.Database.GetDbConnection().QuerySingleAsync<Guid>(
                "select id from communications.notification where tenant_id = @TenantId", new { fixture.TenantId });
        }

        var worker = provider.GetServices<IHostedService>().Single(item => item.GetType().Name == "OutboxProcessingWorker");
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForStatusAsync(provider, new OperationalScope(fixture.TenantId, Guid.NewGuid(), fixture.EstablishmentId), notificationId,
                NotificationDeliveryStatus.AcceptedByProvider);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        Assert.Equal(OrderStatus.Confirmed, (await verifyContext.Orders.SingleAsync(item => item.Id == fixture.OrderId)).Status);
        var history = await verifyScope.ServiceProvider.GetRequiredService<INotificationReadGateway>()
            .ListHistoryAsync(new OperationalScope(fixture.TenantId, Guid.NewGuid(), fixture.EstablishmentId), 1, 10, null, null, CancellationToken.None);
        Assert.Equal(NotificationDeliveryStatus.AcceptedByProvider, Assert.Single(history).Status);
        Assert.Equal(2, sender.Calls);
    }

    [Fact]
    public async Task Customer_order_notification_without_consent_is_recorded_as_blocked_without_changing_order()
    {
        var sender = new FakeNotificationSender(NotificationChannel.Email,
            new NotificationProviderResult(NotificationProviderOutcome.Accepted, "must-not-send", null));
        var services = CreateServices(sender);
        await using var provider = services.BuildServiceProvider();
        var fixture = await CreatePublicOrderAsync(provider, "order.confirmed", "Pedido {{orderNumber}}", true, true, requiresConsent: true);
        Guid notificationId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var order = await context.Orders.SingleAsync(item => item.Id == fixture.OrderId);
            await scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>().StageAsync(order, CancellationToken.None);
            await context.SaveChangesAsync();
            notificationId = await context.Database.GetDbConnection().QuerySingleAsync<Guid>(
                "select id from communications.notification where tenant_id = @TenantId", new { fixture.TenantId });
        }

        var worker = provider.GetServices<IHostedService>().Single(item => item.GetType().Name == "OutboxProcessingWorker");
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForStatusAsync(provider, new OperationalScope(fixture.TenantId, Guid.NewGuid(), fixture.EstablishmentId), notificationId,
                NotificationDeliveryStatus.BlockedByConsent);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        Assert.Equal(OrderStatus.Confirmed, (await verifyContext.Orders.SingleAsync(item => item.Id == fixture.OrderId)).Status);
        Assert.Equal(0, sender.Calls);
    }

    [Theory]
    [InlineData(NotificationProviderOutcome.PermanentFailure, NotificationDeliveryStatus.Failed)]
    [InlineData(NotificationProviderOutcome.Uncertain, NotificationDeliveryStatus.Uncertain)]
    public async Task Customer_order_terminal_or_uncertain_delivery_is_visible_without_changing_order(
        NotificationProviderOutcome outcome, NotificationDeliveryStatus expectedStatus)
    {
        var sender = new FakeNotificationSender(NotificationChannel.Email,
            new NotificationProviderResult(outcome, null, "smtp_result"));
        var services = CreateServices(sender);
        await using var provider = services.BuildServiceProvider();
        var fixture = await CreatePublicOrderAsync(provider, "order.confirmed", "Pedido {{orderNumber}}", true, true);
        Guid notificationId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var order = await context.Orders.SingleAsync(item => item.Id == fixture.OrderId);
            await scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationStager>().StageAsync(order, CancellationToken.None);
            await context.SaveChangesAsync();
            notificationId = await context.Database.GetDbConnection().QuerySingleAsync<Guid>(
                "select id from communications.notification where tenant_id = @TenantId", new { fixture.TenantId });
        }

        var worker = provider.GetServices<IHostedService>().Single(item => item.GetType().Name == "OutboxProcessingWorker");
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForStatusAsync(provider, new OperationalScope(fixture.TenantId, Guid.NewGuid(), fixture.EstablishmentId), notificationId, expectedStatus);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        Assert.Equal(OrderStatus.Confirmed, (await verifyContext.Orders.SingleAsync(item => item.Id == fixture.OrderId)).Status);
        var history = await verifyScope.ServiceProvider.GetRequiredService<INotificationReadGateway>()
            .ListHistoryAsync(new OperationalScope(fixture.TenantId, Guid.NewGuid(), fixture.EstablishmentId), 1, 10, null, null, CancellationToken.None);
        Assert.Equal(expectedStatus, Assert.Single(history).Status);
    }

    private async Task<CustomerOrderFixture> CreatePublicOrderAsync(IServiceProvider provider, string purpose, string body, bool hasEmail, bool hasTemplate, bool requiresConsent = false)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        await context.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Customer notifications", now);
        var establishment = Establishment.Create(tenant.Id, "Customer notification unit", new Slug("customer-notify-" + Guid.NewGuid().ToString("N")), now);
        var customer = Customer.Create(tenant.Id, establishment.Id, "Ana Silva", "+5511999999999", hasEmail ? "customer@example.test" : null, now);
        var order = Order.Create(tenant.Id, establishment.Id, OrderServiceType.Pickup, customer.Id, customer.Name, customer.Phone, null, null, now);
        order.AddItem(Guid.NewGuid(), null, "Pizza", null, new Money(45.90m), new Quantity(1), [], null, now);
        var number = Random.Shared.Next(100000, 999999);
        order.Confirm(number, now);
        context.AddRange(tenant, establishment, customer, order);
        await context.SaveChangesAsync();
        context.PublicOrderRequests.Add(PublicOrderRequest.Create(tenant.Id, establishment.Id,
            "customer-order-key-" + Guid.NewGuid().ToString("N"), new string('a', 64), order.Id, now));
        await context.SaveChangesAsync();
        if (hasTemplate)
        {
            var repository = scope.ServiceProvider.GetRequiredService<INotificationWriteRepository>();
            await repository.UpsertTemplateAsync(new OperationalScope(tenant.Id, Guid.NewGuid(), establishment.Id),
                new(null, purpose, NotificationChannel.Email, "pt_BR", "Atualização do pedido", body, null, requiresConsent, true), now, CancellationToken.None);
        }

        return new CustomerOrderFixture(tenant.Id, establishment.Id, customer.Id, order.Id, order.PublicReference!, number);
    }

    private sealed record CustomerOrderFixture(Guid TenantId, Guid EstablishmentId, Guid CustomerId, Guid OrderId, string PublicReference, long OrderNumber);

    private ServiceCollection CreateServices(FakeNotificationSender sender, int retryDelaySeconds = 30)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = database.GetConnectionString(),
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Outbox:CleanupIntervalHours"] = "24",
            ["NotificationDelivery:MaximumAttempts"] = "2",
            ["NotificationDelivery:InitialRetryDelaySeconds"] = retryDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["NotificationDelivery:MaximumRetryDelaySeconds"] = retryDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        services.RemoveAll<INotificationChannelSender>();
        services.AddSingleton<INotificationChannelSender>(sender);
        return services;
    }

    private static async Task<(OperationalScope Scope, Guid NotificationId)> CreateRequestAsync(
        ServiceProvider provider, NotificationChannel channel, string destination, bool requiresConsent)
    {
        await using var serviceScope = provider.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        await context.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Notification tenant", now);
        var establishment = Establishment.Create(tenant.Id, "Notification unit", new Slug("notify-" + Guid.NewGuid().ToString("N")), now);
        context.AddRange(tenant, establishment);
        await context.SaveChangesAsync();
        var scope = new OperationalScope(tenant.Id, Guid.NewGuid(), establishment.Id);
        var repository = serviceScope.ServiceProvider.GetRequiredService<INotificationWriteRepository>();
        var templateId = await repository.UpsertTemplateAsync(scope,
            new NotificationTemplateDraft(null, "order.update", channel, "pt_BR", channel == NotificationChannel.Email ? "Pedido atualizado" : "",
                "Pedido {{1}} atualizado", channel == NotificationChannel.WhatsApp ? "order_update" : null,
                requiresConsent, true), now, CancellationToken.None);
        var template = await repository.FindTemplateAsync(scope, templateId, CancellationToken.None);
        Assert.NotNull(template);
        var first = await repository.RequestAsync(scope, new(establishment.Id, templateId, destination,
            new Dictionary<string, string> { ["1"] = "42" }, "request-42"), template!, "{\"1\":\"42\"}", now, CancellationToken.None);
        var replay = await repository.RequestAsync(scope, new(establishment.Id, templateId, destination,
            new Dictionary<string, string> { ["1"] = "42" }, "request-42"), template!, "{\"1\":\"42\"}", now, CancellationToken.None);
        Assert.True(first.Created);
        Assert.False(replay.Created);
        Assert.Equal(first.Id, replay.Id);
        return (scope, first.Id);
    }

    private static async Task WaitForStatusAsync(IServiceProvider provider, OperationalScope scope, Guid notificationId,
        NotificationDeliveryStatus expected)
    {
        var until = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < until)
        {
            await using var child = provider.CreateAsyncScope();
            var connection = child.ServiceProvider.GetRequiredService<ZFomeDbContext>().Database.GetDbConnection();
            var state = await connection.QuerySingleOrDefaultAsync<string>(
                "select status from communications.notification where tenant_id = @TenantId and establishment_id = @EstablishmentId and id = @Id",
                new { scope.TenantId, scope.EstablishmentId, Id = notificationId });
            if (Enum.TryParse<NotificationDeliveryStatus>(state, out var parsed) && parsed == expected) return;
            await Task.Delay(100);
        }
        Assert.Fail($"Notification did not reach status {expected}.");
    }

    private static async Task WaitForMailpitSubjectAsync(string mailpitUrl, string subject, string recipient)
    {
        using var client = new HttpClient { BaseAddress = new Uri(mailpitUrl.TrimEnd('/') + "/") };
        var until = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < until)
        {
            using var response = await client.GetAsync("api/v1/messages?limit=100");
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (TryGetProperty(document.RootElement, "messages", out var messages) && messages.ValueKind == JsonValueKind.Array
                && messages.EnumerateArray().Any(message =>
                    TryGetProperty(message, "subject", out var messageSubject) && messageSubject.GetString() == subject
                    && TryGetProperty(message, "to", out var recipients) && recipients.ValueKind == JsonValueKind.Array
                    && recipients.EnumerateArray().Any(address => TryGetProperty(address, "address", out var value) && value.GetString() == recipient)
                    && TryGetProperty(message, "snippet", out var snippet)
                    && snippet.GetString()?.Contains("http://localhost:9000/order/track/", StringComparison.Ordinal) == true))
                return;
            await Task.Delay(250);
        }
        Assert.Fail($"Mailpit did not show the expected customer order message: {subject}.");
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private sealed class FakeNotificationSender(NotificationChannel channel, params NotificationProviderResult[] results) : INotificationChannelSender
    {
        public NotificationChannel Channel => channel;
        public int Calls { get; private set; }
        public Task<NotificationProviderResult> SendAsync(NotificationChannelSendRequest request, CancellationToken cancellationToken)
        {
            Assert.Equal(channel, request.Channel);
            var result = results[Math.Min(Calls, results.Length - 1)];
            Calls++;
            return Task.FromResult(result);
        }
    }
}
