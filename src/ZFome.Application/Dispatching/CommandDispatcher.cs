using ZFome.Application.Abstractions.Commands;

namespace ZFome.Application.Dispatching;

public sealed class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
{
    /// <summary>Valida e encaminha um comando sem retorno ao seu único handler registrado.</summary>
    public async Task DispatchAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);
        await RequestValidator.ValidateAsync(serviceProvider, command, cancellationToken);
        var handler = HandlerResolver.ResolveSingle<ICommandHandler<TCommand>>(serviceProvider);
        await handler.HandleAsync(command, cancellationToken);
    }

    /// <summary>Valida e encaminha um comando ao handler registrado, retornando seu resultado.</summary>
    public async Task<TResult> DispatchAsync<TCommand, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(command);
        await RequestValidator.ValidateAsync(serviceProvider, command, cancellationToken);
        var handler = HandlerResolver.ResolveSingle<ICommandHandler<TCommand, TResult>>(serviceProvider);
        return await handler.HandleAsync(command, cancellationToken);
    }
}
