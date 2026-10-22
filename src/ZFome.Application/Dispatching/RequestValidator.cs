using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ApplicationValidationException = ZFome.Application.Exceptions.ValidationException;

namespace ZFome.Application.Dispatching;

internal static class RequestValidator
{
    public static async Task ValidateAsync<TRequest>(
        IServiceProvider serviceProvider,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var validators = serviceProvider.GetServices<IValidator<TRequest>>().ToArray();
        if (validators.Length == 0)
        {
            return;
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(result => result.Errors).Where(failure => failure is not null).ToArray();

        if (failures.Length > 0)
        {
            throw new ApplicationValidationException(failures);
        }
    }
}
