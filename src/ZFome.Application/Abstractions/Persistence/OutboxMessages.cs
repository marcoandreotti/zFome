namespace ZFome.Application.Abstractions.Persistence;

/// <summary>Mensagem de integração versionada que será persistida junto à unidade de trabalho atual.</summary>
public sealed record OutboxMessageDraft(
    Guid TenantId,
    string MessageType,
    int SchemaVersion,
    string IdempotencyKey,
    DateTimeOffset OccurredAtUtc,
    string PayloadJson,
    DateTimeOffset? NotBeforeUtc = null);

/// <summary>Mensagem persistida disponível para um consumidor idempotente.</summary>
public sealed record OutboxMessageEnvelope(
    Guid Id,
    Guid TenantId,
    string MessageType,
    int SchemaVersion,
    string IdempotencyKey,
    DateTimeOffset OccurredAtUtc,
    string PayloadJson);

/// <summary>Estagia mensagem no mesmo DbContext da gravação de negócio; não confirma a transação.</summary>
public interface IOutboxMessageStager
{
    void Stage(OutboxMessageDraft message);
}

/// <summary>
/// Consumidor de uma versão específica de um contrato de integração. Implementações devem ser idempotentes,
/// pois o worker pode entregar novamente se o processo cair depois do efeito e antes da confirmação.
/// </summary>
public interface IOutboxMessageHandler
{
    string ConsumerName { get; }
    string MessageType { get; }
    int SchemaVersion { get; }
    Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken);
}
