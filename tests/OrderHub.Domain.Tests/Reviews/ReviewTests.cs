using OrderHub.Domain.Exceptions;
using OrderHub.Domain.Reviews;

namespace OrderHub.Domain.Tests.Reviews;

public sealed class ReviewTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid EstablishmentId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-20T12:00:00Z");

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Rating_accepts_only_supported_integer_values(int value) => Assert.Equal(value, Rating.Create(value).Value);

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Rating_rejects_values_outside_range(int value) => Assert.Throws<DomainException>(() => Rating.Create(value));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Low_rating_requires_normalized_comment(int rating)
    {
        Assert.Throws<DomainException>(() => Create(rating, "   "));
        Assert.Throws<DomainException>(() => Create(rating, "curto"));
        Assert.Equal("ruim, precisa melhorar", Create(rating, "  ruim, precisa melhorar  ").Comment);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void High_rating_may_omit_comment(int rating) => Assert.Null(Create(rating, null).Comment);

    [Fact]
    public void Comment_has_maximum_length()
    {
        Assert.Throws<DomainException>(() => Create(5, new string('a', 1001)));
        Assert.Equal(1000, Create(5, new string('a', 1000)).Comment!.Length);
    }

    [Fact]
    public void Establishment_target_must_match_scope()
    {
        Assert.Throws<DomainException>(() => Review.Submit(TenantId, EstablishmentId, OrderId,
            ReviewTargetKind.Establishment, Guid.NewGuid(), 5, null, Now));
    }

    [Fact]
    public void Moderation_hides_and_restores_without_changing_rating()
    {
        var review = Create(2, "needs attention");
        Assert.Equal(ReviewStatus.Pending, review.Status);
        review.Approve(Now.AddSeconds(1));
        Assert.Equal(ReviewStatus.Published, review.Status);
        review.Hide("Review moderation", Now.AddMinutes(1));
        Assert.Equal(ReviewStatus.Hidden, review.Status);
        Assert.Equal("Review moderation", review.ModerationReason);
        Assert.Equal(2, review.Rating.Value);

        review.Restore(Now.AddMinutes(2));
        Assert.Equal(ReviewStatus.Published, review.Status);
        Assert.Null(review.ModerationReason);
        Assert.Throws<DomainException>(() => review.Restore(Now.AddMinutes(3)));
    }

    [Fact]
    public void Submitted_review_waits_for_approval_before_publication()
    {
        var review = Create(5, null);
        Assert.Equal(ReviewStatus.Pending, review.Status);
        Assert.Throws<DomainException>(() => review.Hide("", Now.AddMinutes(1)));
        review.Approve(Now.AddMinutes(2));
        Assert.Equal(ReviewStatus.Published, review.Status);
        Assert.Throws<DomainException>(() => review.Approve(Now.AddMinutes(3)));
    }

    [Fact]
    public void Rejected_review_cannot_be_restored_or_published()
    {
        var review = Create(5, null);
        review.Reject("Contains personal information", Now.AddMinutes(1));

        Assert.Equal(ReviewStatus.Rejected, review.Status);
        Assert.Equal("Contains personal information", review.ModerationReason);
        Assert.Throws<DomainException>(() => review.Restore(Now.AddMinutes(2)));
        Assert.Throws<DomainException>(() => review.Approve(Now.AddMinutes(2)));
    }

    private static Review Create(int rating, string? comment) =>
        Review.Submit(TenantId, EstablishmentId, OrderId, ReviewTargetKind.Establishment,
            EstablishmentId, rating, comment, Now);
}

