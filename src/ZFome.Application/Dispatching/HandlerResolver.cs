using Microsoft.Extensions.DependencyInjection;

namespace ZFome.Application.Dispatching;

internal static class HandlerResolver
{
    public static THandler ResolveSingle<THandler>(IServiceProvider serviceProvider)
        where THandler : notnull
    {
        var handlers = serviceProvider.GetServices<THandler>().ToArray();

        return handlers.Length switch
        {
            1 => handlers[0],
            0 => throw new InvalidOperationException($"No handler is registered for {typeof(THandler).Name}."),
            _ => throw new InvalidOperationException($"Multiple handlers are registered for {typeof(THandler).Name}.")
        };
    }
}
