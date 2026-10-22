using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Dispatching;
using ApplicationValidationException = ZFome.Application.Exceptions.ValidationException;

namespace ZFome.Application.Tests.Dispatching;

public sealed class DispatchersTests
{
    [Fact]
    public async Task Command_without_result_invokes_handler()
    {
        var handler = new CommandHandler();
        await using var provider = BuildProvider(services =>
            services.AddSingleton<ICommandHandler<TestCommand>>(handler));

        await new CommandDispatcher(provider).DispatchAsync(new TestCommand("valid"), CancellationToken.None);

        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task Command_with_result_returns_handler_result()
    {
        await using var provider = BuildProvider(services =>
            services.AddSingleton<ICommandHandler<ResultCommand, int>>(new ResultCommandHandler()));

        var result = await new CommandDispatcher(provider)
            .DispatchAsync<ResultCommand, int>(new ResultCommand(21), CancellationToken.None);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Query_returns_read_model()
    {
        await using var provider = BuildProvider(services =>
            services.AddSingleton<IQueryHandler<TestQuery, string>>(new QueryHandler()));

        var result = await new QueryDispatcher(provider)
            .DispatchAsync<TestQuery, string>(new TestQuery(7), CancellationToken.None);

        Assert.Equal("item-7", result);
    }

    [Fact]
    public async Task Invalid_request_does_not_execute_handler()
    {
        var handler = new CommandHandler();
        await using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand>>(handler);
            services.AddSingleton<IValidator<TestCommand>>(new TestCommandValidator());
        });

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            new CommandDispatcher(provider).DispatchAsync(new TestCommand(string.Empty), CancellationToken.None));

        Assert.False(handler.WasCalled);
        Assert.Contains(nameof(TestCommand.Name), exception.Errors.Keys);
    }

    [Fact]
    public async Task Cancellation_token_is_propagated_to_handler()
    {
        using var source = new CancellationTokenSource();
        var handler = new CancellationAwareHandler(source.Token);
        await using var provider = BuildProvider(services =>
            services.AddSingleton<ICommandHandler<TestCommand>>(handler));

        await new CommandDispatcher(provider).DispatchAsync(new TestCommand("valid"), source.Token);

        Assert.True(handler.ReceivedExpectedToken);
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed record TestCommand(string Name) : ICommand;
    private sealed record ResultCommand(int Value) : ICommand<int>;
    private sealed record TestQuery(int Id) : IQuery<string>;

    private sealed class CommandHandler : ICommandHandler<TestCommand>
    {
        public bool WasCalled { get; private set; }

        public Task HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ResultCommandHandler : ICommandHandler<ResultCommand, int>
    {
        public Task<int> HandleAsync(ResultCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(command.Value * 2);
    }

    private sealed class QueryHandler : IQueryHandler<TestQuery, string>
    {
        public Task<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
            Task.FromResult($"item-{query.Id}");
    }

    private sealed class CancellationAwareHandler(CancellationToken expectedToken) : ICommandHandler<TestCommand>
    {
        public bool ReceivedExpectedToken { get; private set; }

        public Task HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            ReceivedExpectedToken = cancellationToken == expectedToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator() => RuleFor(command => command.Name).NotEmpty();
    }
}
