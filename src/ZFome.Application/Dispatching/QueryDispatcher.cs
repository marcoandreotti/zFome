using ZFome.Application.Abstractions.Queries;

namespace ZFome.Application.Dispatching;

public sealed class QueryDispatcher(IServiceProvider serviceProvider) : IQueryDispatcher
{
    /// <summary>Valida e encaminha uma consulta ao seu único handler registrado.</summary>
    public async Task<TResult> DispatchAsync<TQuery, TResult>(
        TQuery query,
        CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
    {
        ArgumentNullException.ThrowIfNull(query);
        await RequestValidator.ValidateAsync(serviceProvider, query, cancellationToken);
        var handler = HandlerResolver.ResolveSingle<IQueryHandler<TQuery, TResult>>(serviceProvider);
        return await handler.HandleAsync(query, cancellationToken);
    }
}
