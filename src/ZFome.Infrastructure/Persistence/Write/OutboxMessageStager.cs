using System.Text.Json;
using ZFome.Application.Abstractions.Persistence;

namespace ZFome.Infrastructure.Persistence.Write;

internal sealed class OutboxMessageStager(ZFomeDbContext context, TimeProvider timeProvider) : IOutboxMessageStager
{
    public void Stage(OutboxMessageDraft message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required for an outbox message.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.MessageType) || message.MessageType.Length > 200)
        {
            throw new ArgumentException("MessageType must contain between 1 and 200 characters.", nameof(message));
        }

        if (message.SchemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "SchemaVersion must be positive.");
        }

        if (message.OccurredAtUtc == default)
        {
            throw new ArgumentException("OccurredAtUtc is required for an outbox message.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.IdempotencyKey) || message.IdempotencyKey.Length > 200)
        {
            throw new ArgumentException("IdempotencyKey must contain between 1 and 200 characters.", nameof(message));
        }

        try
        {
            using var _ = JsonDocument.Parse(message.PayloadJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("PayloadJson must contain a valid JSON value.", nameof(message), exception);
        }

        var existing = context.OutboxMessages.Local.SingleOrDefault(
            x => x.TenantId == message.TenantId && x.IdempotencyKey == message.IdempotencyKey);
        if (existing is not null)
        {
            if (existing.MessageType != message.MessageType
                || existing.SchemaVersion != message.SchemaVersion
                || existing.PayloadJson != message.PayloadJson)
            {
                throw new InvalidOperationException("An outbox idempotency key cannot be reused for different message content.");
            }

            return;
        }

        context.OutboxMessages.Add(new OutboxMessageRecord(
            Guid.NewGuid(),
            message.TenantId,
            message.MessageType,
            message.SchemaVersion,
            message.IdempotencyKey,
            message.OccurredAtUtc.ToUniversalTime(),
            message.PayloadJson,
            timeProvider.GetUtcNow(),
            message.NotBeforeUtc));
    }
}
