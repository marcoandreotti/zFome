using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Infrastructure.Persistence;

internal sealed class OutboxProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxProcessingOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextCleanup = timeProvider.GetUtcNow();

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.Value;
            IReadOnlyList<ClaimedOutboxMessage> messages = [];
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<OutboxMessageStore>();
                var now = timeProvider.GetUtcNow();
                if (now >= nextCleanup)
                {
                    var deletedCount = await store.DeleteExpiredAsync(now, stoppingToken);
                    if (deletedCount > 0)
                    {
                        logger.LogInformation("Deleted {Count} expired outbox messages.", deletedCount);
                    }

                    nextCleanup = now.AddHours(settings.CleanupIntervalHours);
                }

                messages = await store.ClaimBatchAsync(now, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox polling failed with {ErrorType}.", exception.GetType().Name);
            }

            foreach (var claimedMessage in messages)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<OutboxMessageStore>();
                var message = claimedMessage.Message;
                var handlers = scope.ServiceProvider.GetServices<IOutboxMessageHandler>()
                    .Where(handler => handler.MessageType == message.MessageType && handler.SchemaVersion == message.SchemaVersion)
                    .ToArray();
                try
                {
                    await store.ProcessAsync(claimedMessage, handlers, timeProvider.GetUtcNow(), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    await store.MarkFailedAsync(message.Id, claimedMessage.LeaseToken, claimedMessage.AttemptCount, exception, timeProvider.GetUtcNow(), stoppingToken);
                    logger.LogWarning(
                        "Outbox message {MessageId} ({MessageType} v{SchemaVersion}) failed with {ErrorType}.",
                        message.Id,
                        message.MessageType,
                        message.SchemaVersion,
                        exception.GetType().Name);
                }
            }

            if (messages.Count == settings.BatchSize)
            {
                continue;
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.PollingIntervalSeconds), timeProvider, stoppingToken);
        }
    }
}
