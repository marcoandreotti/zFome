using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ZFome.Application.Abstractions.Persistence;

namespace ZFome.Infrastructure.Persistence.Write;

internal sealed record ClaimedOutboxMessage(OutboxMessageEnvelope Message, int AttemptCount, Guid LeaseToken);

internal sealed class OutboxMessageStore(ZFomeDbContext context, OutboxProcessingOptions options)
{
    public async Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimBatchAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            with claimable as (
                select id
                from integration.outbox_message
                where processed_at_utc is null
                  and dead_lettered_at_utc is null
                  and next_attempt_at_utc <= @Now
                  and (lease_until_utc is null or lease_until_utc <= @Now)
                  and attempt_count < @MaxAttempts
                order by created_at_utc, id
                limit @BatchSize
                for update skip locked
            )
            update integration.outbox_message as message
            set lease_token = @LeaseToken,
                lease_until_utc = @LeaseUntil,
                attempt_count = message.attempt_count + 1,
                last_attempt_at_utc = @Now
            from claimable
            where message.id = claimable.id
            returning message.id,
                message.tenant_id as TenantId,
                message.message_type as MessageType,
                message.schema_version as SchemaVersion,
                message.idempotency_key as IdempotencyKey,
                message.occurred_at_utc as OccurredAtUtc,
                message.payload_json::text as PayloadJson,
                message.attempt_count as AttemptCount,
                message.lease_token as LeaseToken;
            """;

        var leaseToken = Guid.NewGuid();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        var rows = await connection.QueryAsync<ClaimedOutboxMessageRow>(new CommandDefinition(
            sql,
            new
            {
                Now = now,
                MaxAttempts = options.MaxAttempts,
                BatchSize = options.BatchSize,
                LeaseToken = leaseToken,
                LeaseUntil = now.AddSeconds(options.LeaseDurationSeconds)
            },
            transaction.GetDbTransaction(),
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(row => row.ToClaimedMessage()).ToArray();
    }

    public async Task ProcessAsync(
        ClaimedOutboxMessage claimedMessage,
        IReadOnlyList<IOutboxMessageHandler> handlers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = claimedMessage.Message;
        if (handlers.Count == 0)
        {
            throw new InvalidOperationException($"No handler is registered for message {message.MessageType} version {message.SchemaVersion}.");
        }

        if (handlers.Any(handler => string.IsNullOrWhiteSpace(handler.ConsumerName) || handler.ConsumerName.Length > 200)
            || handlers.Select(handler => handler.ConsumerName).Distinct(StringComparer.Ordinal).Count() != handlers.Count)
        {
            throw new InvalidOperationException("Outbox consumer names must be unique and contain between 1 and 200 characters.");
        }

        const string insertReceiptSql = """
            insert into integration.outbox_consumer_receipt (tenant_id, message_id, consumer_name, processed_at_utc)
            values (@TenantId, @MessageId, @ConsumerName, @ProcessedAtUtc)
            on conflict (tenant_id, message_id, consumer_name) do nothing
            returning message_id;
            """;
        const string markProcessedSql = """
            update integration.outbox_message
            set processed_at_utc = @Now,
                lease_token = null,
                lease_until_utc = null,
                last_error = null
            where id = @MessageId and lease_token = @LeaseToken and processed_at_utc is null and dead_lettered_at_utc is null;
            """;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        foreach (var handler in handlers)
        {
            var receiptId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                insertReceiptSql,
                new
                {
                    message.TenantId,
                    MessageId = message.Id,
                    handler.ConsumerName,
                    ProcessedAtUtc = now
                },
                transaction.GetDbTransaction(),
                cancellationToken: cancellationToken));
            if (receiptId.HasValue)
            {
                await handler.HandleAsync(message, cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        var marked = await connection.ExecuteAsync(new CommandDefinition(
            markProcessedSql,
            new { Now = now, MessageId = message.Id, claimedMessage.LeaseToken },
            transaction.GetDbTransaction(),
            cancellationToken: cancellationToken));
        if (marked != 1)
        {
            throw new InvalidOperationException("Outbox message lease was lost before processing could be committed.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid messageId,
        Guid leaseToken,
        int attemptCount,
        Exception exception,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            update integration.outbox_message
            set dead_lettered_at_utc = case when attempt_count >= @MaxAttempts then @Now else null end,
                next_attempt_at_utc = @NextAttemptAt,
                lease_token = null,
                lease_until_utc = null,
                last_error = @LastError
            where id = @MessageId and lease_token = @LeaseToken and processed_at_utc is null and dead_lettered_at_utc is null;
            """;
        var exponent = Math.Clamp(attemptCount - 1, 0, 30);
        var delaySeconds = Math.Min(
            options.MaximumRetryDelaySeconds,
            options.InitialRetryDelaySeconds * Math.Pow(2, exponent));
        await ExecuteAsync(sql, new
        {
            MessageId = messageId,
            LeaseToken = leaseToken,
            MaxAttempts = options.MaxAttempts,
            Now = now,
            NextAttemptAt = now.AddSeconds(delaySeconds),
            LastError = exception.GetType().Name
        }, cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = """
            delete from integration.outbox_message
            where processed_at_utc < @Cutoff
               or dead_lettered_at_utc < @Cutoff;
            """;
        var cutoff = now.AddDays(-options.RetentionDays);
        return await ExecuteAsync(sql, new { Cutoff = cutoff }, cancellationToken);
    }

    private async Task<int> ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    private sealed class ClaimedOutboxMessageRow
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string MessageType { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public Guid LeaseToken { get; set; }

        public ClaimedOutboxMessage ToClaimedMessage() => new(
            new OutboxMessageEnvelope(
                Id,
                TenantId,
                MessageType,
                SchemaVersion,
                IdempotencyKey,
                new DateTimeOffset(DateTime.SpecifyKind(OccurredAtUtc, DateTimeKind.Utc)),
                PayloadJson),
            AttemptCount,
            LeaseToken);
    }
}
