using ZFome.Application.Reviews;

namespace ZFome.Application.Tests.Reviews;

public sealed class PublishReviewsValidatorTests
{
    private readonly PublishReviewsValidator validator = new();

    [Fact]
    public void Accepts_low_rating_only_with_minimum_comment()
    {
        var valid = new PublishReviewsCommand(new string('a', 48), "idempotency-123", 2,
            "Experiência ruim", []);
        Assert.True(validator.Validate(valid).IsValid);

        var invalid = valid with { EstablishmentComment = "curto" };
        Assert.False(validator.Validate(invalid).IsValid);
    }

    [Fact]
    public void Rejects_invalid_reference_rating_and_long_comment()
    {
        var command = new PublishReviewsCommand("not-a-reference", "", 6,
            new string('x', 1001), []);
        var result = validator.Validate(command);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Reference));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.EstablishmentRating));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.EstablishmentComment));
    }
}
