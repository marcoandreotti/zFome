using ZFome.Application.Ordering;
using ZFome.Domain.Ordering;

namespace ZFome.Application.Tests.Ordering;

public sealed class OrderSchedulingValidationTests
{
    [Fact]
    public async Task Scheduling_horizon_validation_is_in_portuguese()
    {
        var validator = new SetOrderSchedulingPolicyCommandValidator();
        var command = new SetOrderSchedulingPolicyCommand(
            Guid.NewGuid(), OrderServiceType.Pickup, true, 1_441, 1, null);

        var result = await validator.ValidateAsync(command);

        Assert.Contains(result.Errors, failure => failure.ErrorMessage ==
            "O prazo mínimo de antecedência não pode exceder o horizonte de agendamento.");
    }
}
