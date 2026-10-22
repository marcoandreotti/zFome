namespace ZFome.Application.Abstractions.Queries;

/// <summary>
/// Representa um despachante de consultas (queries) que encaminha uma consulta do tipo <typeparamref name="TQuery"/> para o manipulador apropriado e retorna um resultado do tipo <typeparamref name="TResult"/>.
/// </summary>
public interface IQueryDispatcher
{
    Task<TResult> DispatchAsync<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>;
}