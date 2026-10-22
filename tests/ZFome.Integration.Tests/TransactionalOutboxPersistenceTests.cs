using Dapper;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class TransactionalOutboxPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => database.StartAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Outbox_messages_are_versioned_idempotent_and_atomic_with_business_data()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = database.GetConnectionString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var stager = scope.ServiceProvider.GetRequiredService<IOutboxMessageStager>();
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var firstTenant = Tenant.Create("Outbox first", now);
        var secondTenant = Tenant.Create("Outbox second", now);
        context.AddRange(firstTenant, secondTenant);
        stager.Stage(new OutboxMessageDraft(firstTenant.Id, "orders.confirmed", 1, "order-42-confirmed", now, "{\"orderId\":42}"));
        stager.Stage(new OutboxMessageDraft(firstTenant.Id, "orders.confirmed", 1, "order-42-confirmed", now, "{\"orderId\":42}"));
        stager.Stage(new OutboxMessageDraft(secondTenant.Id, "orders.confirmed", 1, "order-42-confirmed", now, "{\"orderId\":42}"));
        await context.SaveChangesAsync();

        var connection = context.Database.GetDbConnection();
        var persisted = (await connection.QueryAsync<(Guid TenantId, string MessageType, int SchemaVersion, string IdempotencyKey, string PayloadJson)>(
            "select tenant_id as TenantId, message_type as MessageType, schema_version as SchemaVersion, idempotency_key as IdempotencyKey, payload_json::text as PayloadJson from integration.outbox_message order by tenant_id;"))
            .ToArray();

        Assert.Equal(2, persisted.Length);
        Assert.All(persisted, message =>
        {
            Assert.Equal("orders.confirmed", message.MessageType);
            Assert.Equal(1, message.SchemaVersion);
            Assert.Equal("order-42-confirmed", message.IdempotencyKey);
            Assert.Equal(42, JsonDocument.Parse(message.PayloadJson).RootElement.GetProperty("orderId").GetInt32());
        });
        Assert.Contains(persisted, message => message.TenantId == firstTenant.Id);
        Assert.Contains(persisted, message => message.TenantId == secondTenant.Id);

        var rolledBackTenant = Tenant.Create("Outbox rollback", now);
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            context.Add(rolledBackTenant);
            stager.Stage(new OutboxMessageDraft(rolledBackTenant.Id, "orders.confirmed", 1, "rolled-back", now, "{}"));
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verification = new ZFomeDbContext(new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options);
        Assert.False(await verification.Tenants.AnyAsync(tenant => tenant.Id == rolledBackTenant.Id));
        var rolledBackMessages = await verification.Database.GetDbConnection().ExecuteScalarAsync<int>(
            "select count(*) from integration.outbox_message where tenant_id = @TenantId;",
            new { TenantId = rolledBackTenant.Id });
        Assert.Equal(0, rolledBackMessages);
    }

    [Fact]
    public async Task Worker_retries_with_idempotent_consumers_and_dead_letters_after_the_limit()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Outbox:BatchSize"] = "10",
            ["Outbox:MaxAttempts"] = "2",
            ["Outbox:LeaseDurationSeconds"] = "10",
            ["Outbox:PollingIntervalSeconds"] = "1",
            ["Outbox:InitialRetryDelaySeconds"] = "1",
            ["Outbox:MaximumRetryDelaySeconds"] = "1"
        });
        var handler = new RetryAndPermanentFailureHandler();
        var services = CreateServices(configuration, handler);
        await using var provider = services.BuildServiceProvider();

        await StageTwoMessagesAsync(provider);
        var registeredHandlers = provider.GetServices<IOutboxMessageHandler>().ToArray();
        var testHandler = Assert.Single(registeredHandlers, registered => registered.MessageType == "outbox.test");
        Assert.Equal(1, testHandler.SchemaVersion);
        var worker = provider.GetServices<IHostedService>().Single(service => service.GetType().Name == "OutboxProcessingWorker");
        await worker.StartAsync(CancellationToken.None);
        await WaitForTerminalStatesAsync(provider, handler);
        await worker.StopAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var connection = context.Database.GetDbConnection();
        var successfulAttempts = await connection.ExecuteScalarAsync<int>(
            "select attempt_count from integration.outbox_message where idempotency_key = 'transient-failure';");
        var successfulState = await connection.ExecuteScalarAsync<bool>(
            "select processed_at_utc is not null from integration.outbox_message where idempotency_key = 'transient-failure';");
        var terminalAttempts = await connection.ExecuteScalarAsync<int>(
            "select attempt_count from integration.outbox_message where idempotency_key = 'permanent-failure';");
        var terminalState = await connection.ExecuteScalarAsync<bool>(
            "select dead_lettered_at_utc is not null from integration.outbox_message where idempotency_key = 'permanent-failure';");
        var successfulReceipts = await connection.ExecuteScalarAsync<int>(
            "select count(*) from integration.outbox_consumer_receipt where consumer_name = @Consumer and message_id = (select id from integration.outbox_message where idempotency_key = 'transient-failure');",
            new { Consumer = RetryAndPermanentFailureHandler.Name });

        Assert.Equal(2, successfulAttempts);
        Assert.True(successfulState);
        Assert.Equal(2, terminalAttempts);
        Assert.True(terminalState);
        Assert.Equal(1, handler.EffectCount);
        Assert.Equal(1, successfulReceipts);

        handler.EnablePermanentConsumer();
        var requeuedCount = await connection.ExecuteAsync("""
            update integration.outbox_message
            set dead_lettered_at_utc = null,
                attempt_count = 0,
                next_attempt_at_utc = @Now,
                lease_token = null,
                lease_until_utc = null,
                last_error = null
            where idempotency_key = 'permanent-failure' and dead_lettered_at_utc is not null;
            """,
            new { Now = DateTimeOffset.UtcNow });
        Assert.Equal(1, requeuedCount);
        await using var recoveryProvider = CreateServices(configuration, handler).BuildServiceProvider();
        var recoveryWorker = recoveryProvider.GetServices<IHostedService>().Single(service => service.GetType().Name == "OutboxProcessingWorker");
        await recoveryWorker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForProcessedKeyAsync(recoveryProvider, "permanent-failure", handler);
        }
        finally
        {
            await recoveryWorker.StopAsync(CancellationToken.None);
        }
        var recoveredState = await connection.ExecuteScalarAsync<bool>(
            "select processed_at_utc is not null and dead_lettered_at_utc is null and attempt_count = 1 from integration.outbox_message where idempotency_key = 'permanent-failure';");
        var recoveredReceipts = await connection.ExecuteScalarAsync<int>(
            "select count(*) from integration.outbox_consumer_receipt where consumer_name = @Consumer and message_id = (select id from integration.outbox_message where idempotency_key = 'permanent-failure');",
            new { Consumer = RetryAndPermanentFailureHandler.Name });
        Assert.True(recoveredState);
        Assert.Equal(1, recoveredReceipts);
        Assert.Equal(2, handler.EffectCount);
    }

    [Fact]
    public async Task Multiple_workers_claim_disjoint_batches()
    {
        const int messageCount = 12;
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Outbox:BatchSize"] = "2",
            ["Outbox:MaxAttempts"] = "3",
            ["Outbox:LeaseDurationSeconds"] = "10",
            ["Outbox:PollingIntervalSeconds"] = "1"
        });
        var handler = new ConcurrentConsumer(messageCount);
        using var firstHost = CreateHost(configuration, handler);
        using var secondHost = CreateHost(configuration, handler);
        await StageManyMessagesAsync(firstHost.Services, messageCount);

        await firstHost.StartAsync();
        await secondHost.StartAsync();
        try
        {
            await handler.Completed.WaitAsync(TimeSpan.FromSeconds(20));
            await WaitForProcessedCountAsync(firstHost.Services, messageCount);
        }
        finally
        {
            await Task.WhenAll(firstHost.StopAsync(), secondHost.StopAsync());
        }

        await using var scope = firstHost.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var connection = context.Database.GetDbConnection();
        var processedCount = await connection.ExecuteScalarAsync<int>("select count(*) from integration.outbox_message where processed_at_utc is not null;");
        var retriedCount = await connection.ExecuteScalarAsync<int>("select count(*) from integration.outbox_message where attempt_count > 1;");
        Assert.Equal(messageCount, processedCount);
        Assert.Equal(0, retriedCount);
        Assert.Equal(messageCount, handler.EffectCount);
        Assert.Equal(0, handler.DuplicateEffectCount);
    }

    private IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        values["Database:ConnectionString"] = database.GetConnectionString();
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ServiceCollection CreateServices(IConfiguration configuration, IOutboxMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(handler);
        services.AddInfrastructure(configuration);
        return services;
    }

    private async Task StageTwoMessagesAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var stager = scope.ServiceProvider.GetRequiredService<IOutboxMessageStager>();
        await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var transientTenant = Tenant.Create("Outbox transient", now);
        var permanentTenant = Tenant.Create("Outbox permanent", now);
        context.AddRange(transientTenant, permanentTenant);
        stager.Stage(new OutboxMessageDraft(transientTenant.Id, RetryAndPermanentFailureHandler.Message, 1, "transient-failure", now, "{}"));
        stager.Stage(new OutboxMessageDraft(permanentTenant.Id, RetryAndPermanentFailureHandler.Message, 1, "permanent-failure", now, "{}"));
        await context.SaveChangesAsync();
    }

    private async Task StageManyMessagesAsync(IServiceProvider provider, int messageCount)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var stager = scope.ServiceProvider.GetRequiredService<IOutboxMessageStager>();
        await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Outbox concurrency", now);
        context.Add(tenant);
        for (var index = 0; index < messageCount; index++)
        {
            stager.Stage(new OutboxMessageDraft(tenant.Id, ConcurrentConsumer.Message, 1, $"parallel-{index}", now, "{}"));
        }

        await context.SaveChangesAsync();
    }

    private async Task WaitForProcessedCountAsync(IServiceProvider provider, int expectedCount)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var processedCount = await context.Database.GetDbConnection().ExecuteScalarAsync<int>(
                "select count(*) from integration.outbox_message where processed_at_utc is not null;");
            if (processedCount == expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException("The parallel workers did not process the complete outbox batch.");
    }

    private static async Task WaitForProcessedKeyAsync(IServiceProvider provider, string idempotencyKey, RetryAndPermanentFailureHandler handler)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var isProcessed = await context.Database.GetDbConnection().ExecuteScalarAsync<bool>(
                "select processed_at_utc is not null from integration.outbox_message where idempotency_key = @IdempotencyKey;",
                new { IdempotencyKey = idempotencyKey });
            if (isProcessed)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        await using var diagnosticScope = provider.CreateAsyncScope();
        var diagnosticContext = diagnosticScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var state = await diagnosticContext.Database.GetDbConnection().QuerySingleAsync<string>(
            "select attempt_count || ':' || coalesce(dead_lettered_at_utc::text, 'not-terminal') || ':' || coalesce(last_error, 'none') || ':next=' || next_attempt_at_utc::text || ':processed=' || coalesce(processed_at_utc::text, 'no') from integration.outbox_message where idempotency_key = @IdempotencyKey;",
            new { IdempotencyKey = idempotencyKey });
        throw new TimeoutException($"Outbox message {idempotencyKey} was not processed after recovery. State: {state}; handler attempts: {handler.PermanentAttempts}.");
    }

    private IHost CreateHost(IConfiguration configuration, IOutboxMessageHandler handler) => new HostBuilder()
        .ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(handler);
            services.AddInfrastructure(configuration);
        })
        .Build();

    private static async Task WaitForTerminalStatesAsync(IServiceProvider provider, RetryAndPermanentFailureHandler handler)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
            var connection = context.Database.GetDbConnection();
            var successfulState = await connection.ExecuteScalarAsync<bool>(
                "select processed_at_utc is not null from integration.outbox_message where idempotency_key = 'transient-failure';");
            var terminalState = await connection.ExecuteScalarAsync<bool>(
                "select dead_lettered_at_utc is not null from integration.outbox_message where idempotency_key = 'permanent-failure';");
            if (successfulState && terminalState)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        await using var diagnosticScope = provider.CreateAsyncScope();
        var diagnosticContext = diagnosticScope.ServiceProvider.GetRequiredService<ZFomeDbContext>();
        var error = await diagnosticContext.Database.GetDbConnection().QuerySingleAsync<string>(
            "select string_agg(message_type || ':' || schema_version || ':' || idempotency_key || ':' || attempt_count || ':' || coalesce(last_error, 'none'), ', ') from integration.outbox_message;");
        throw new TimeoutException($"Outbox worker did not reach the expected states. Current messages: {error}; handler calls: {handler.TransientAttempts}/{handler.PermanentAttempts}.");
    }

    private sealed class RetryAndPermanentFailureHandler : IOutboxMessageHandler
    {
        public const string Name = "outbox-test-consumer";
        public const string Message = "outbox.test";
        private readonly ConcurrentDictionary<string, byte> effects = new(StringComparer.Ordinal);
        private int transientAttempts;
        private int permanentAttempts;
        private int allowPermanent;

        public string ConsumerName => Name;
        public string MessageType => Message;
        public int SchemaVersion => 1;
        public int EffectCount => effects.Count;
        public int TransientAttempts => Volatile.Read(ref transientAttempts);
        public int PermanentAttempts => Volatile.Read(ref permanentAttempts);

        public void EnablePermanentConsumer() => Volatile.Write(ref allowPermanent, 1);

        public Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken)
        {
            if (message.IdempotencyKey == "transient-failure")
            {
                var attempt = Interlocked.Increment(ref transientAttempts);
                effects.TryAdd(message.IdempotencyKey, 0);
                if (attempt == 1)
                {
                    throw new InvalidOperationException("Simulated failure after idempotent effect.");
                }
            }
            else
            {
                Interlocked.Increment(ref permanentAttempts);
                if (Volatile.Read(ref allowPermanent) == 0)
                {
                    throw new InvalidOperationException("Simulated terminal failure.");
                }

                effects.TryAdd(message.IdempotencyKey, 0);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ConcurrentConsumer(int expectedCount) : IOutboxMessageHandler
    {
        public const string Message = "outbox.parallel";
        private readonly ConcurrentDictionary<string, byte> effects = new(StringComparer.Ordinal);
        private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int duplicateEffectCount;

        public string ConsumerName => "outbox-parallel-consumer";
        public string MessageType => Message;
        public int SchemaVersion => 1;
        public int EffectCount => effects.Count;
        public int DuplicateEffectCount => Volatile.Read(ref duplicateEffectCount);
        public Task Completed => completed.Task;

        public async Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            if (!effects.TryAdd(message.IdempotencyKey, 0))
            {
                Interlocked.Increment(ref duplicateEffectCount);
            }

            if (effects.Count == expectedCount)
            {
                completed.TrySetResult();
            }
        }
    }
}
