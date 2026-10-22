namespace ZFome.Infrastructure.Persistence.Write;

internal sealed class OutboxConsumerReceiptRecord
{
    public Guid TenantId { get; private set; }
    public Guid MessageId { get; private set; }
    public string ConsumerName { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; private set; }
}
