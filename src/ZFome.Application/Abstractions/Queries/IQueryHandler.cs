namespace ZFome.Application.Abstractions.Queries;

/// <summary>
/// Representa um manipulador de consultas (queries) que processa uma consulta do tipo <typeparamref name="TQuery"/> e retorna um resultado do tipo <typeparamref name="TResult"/>.
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}