using ZFome.Application.Onboarding;
using ZFome.Domain.Exceptions;
using ZFome.Domain.Operations;
using ZFome.Domain.Tenancy;

namespace ZFome.Application.Tests.Tenancy;

public sealed class OnboardingTests
{
    [Fact]
    public void Readiness_is_derived_for_new_existing_completed_and_regressed_units()
    {
        var fresh = OnboardingProgress.Calculate("Restaurant", "restaurant", true, 0, 1, 0, null);
        Assert.False(fresh.IsReady); Assert.Equal(["horarios"], fresh.PendingSteps);
        var existing = OnboardingProgress.Calculate("Restaurant", "restaurant", true, 1, 1, 0, null);
        Assert.True(existing.IsReady); Assert.Null(existing.CompletedAt);
        var completedAt = DateTimeOffset.UtcNow;
        var completed = OnboardingProgress.Calculate("Restaurant", "restaurant", true, 1, 1, 0, completedAt);
        Assert.True(completed.IsReady);
        var regressed = OnboardingProgress.Calculate("Restaurant", "restaurant", true, 0, 0, 0, completedAt);
        Assert.False(regressed.IsReady); Assert.Equal(completedAt, regressed.CompletedAt); Assert.Contains("acessos", regressed.PendingSteps);
        Assert.False(OnboardingProgress.Calculate("Restaurant", "restaurant", false, 1, 1, 0, completedAt).IsReady);
    }
    [Fact]
    public void Validators_reject_missing_intents_invalid_days_and_unsafe_theme_assets()
    {
        var unit = Guid.NewGuid();
        Assert.False(new CreateTableValidator().Validate(new CreateTableCommand(unit, Guid.Empty, "A1", null)).IsValid);
        Assert.False(new ReplaceBusinessHoursValidator().Validate(new ReplaceBusinessHoursCommand(unit, [new((DayOfWeek)8, new(9, 0), new(18, 0))])).IsValid);
        Assert.False(new UpdateThemeValidator().Validate(new UpdateThemeCommand(unit, new(LogoUrl: "javascript:alert(1)"))).IsValid);
        Assert.False(new UpdateThemeValidator().Validate(new UpdateThemeCommand(unit, new(PrimaryColor: "red"))).IsValid);
        Assert.True(new UpdateThemeValidator().Validate(new UpdateThemeCommand(unit, new(PrimaryColor: "#aabbcc"))).IsValid);
        Assert.True(new ReplaceBusinessHoursValidator().Validate(new ReplaceBusinessHoursCommand(unit, [])).IsValid);
        Assert.False(new SearchTablesValidator().Validate(new SearchTablesQuery(unit, 1, 101)).IsValid);
    }
}
