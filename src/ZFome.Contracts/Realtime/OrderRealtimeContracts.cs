namespace ZFome.Contracts.Realtime;

public sealed record OrderUpdatedMessageV1(
    Guid OrderId,
    string ChangeType,
    Guid EstablishmentId,
    DateTimeOffset OccurredAt)
{
    public int Version => 1;
}
