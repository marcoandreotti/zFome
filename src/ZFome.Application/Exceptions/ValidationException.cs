using FluentValidation.Results;

namespace ZFome.Application.Exceptions;

public sealed class ValidationException(IEnumerable<ValidationFailure> failures)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = failures
        .GroupBy(failure => failure.PropertyName)
        .ToDictionary(
            group => group.Key,
            group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());
}
