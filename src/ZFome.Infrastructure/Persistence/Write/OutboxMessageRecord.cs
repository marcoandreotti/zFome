namespace ZFome.Infrastructure.Persistence.Write;

internal sealed class OutboxMessageRecord
{
    private OutboxMessageRecord()
    {
    }

    public OutboxMessageRecord(
        Guid id,
        Guid tenantId,
        string messageType,
        int schemaVersion,
        string idempotencyKey,
        DateTimeOffset occurredAtUtc,
        string payloadJson,
        DateTimeOffset now,
        DateTimeOffset? notBeforeUtc = null)
    {
        Id = id;
        TenantId = tenantId;
        MessageType = messageType;
        SchemaVersion = schemaVersion;
        IdempotencyKey = idempotencyKey;
        OccurredAtUtc = occurredAtUtc;
        PayloadJson = payloadJson;
        NextAttemptAtUtc = notBeforeUtc?.ToUniversalTime() ?? now;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntilUtc { get; private set; }
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
