using ZFome.Domain.Exceptions;
using ZFome.Domain.Payments;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Tests.Payments;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    [Fact] public void Method_normalizes_code_and_deactivation_does_not_change_payment_snapshot()
    { var method = Method(" pix-01 "); var payment = Payment.Create(method.TenantId, method.EstablishmentId, Guid.NewGuid(), method, new Money(20), null, Now); method.Deactivate(Now); Assert.Equal("PIX01", payment.PaymentMethodCode); Assert.Equal("Pix", payment.PaymentMethodName); }
    [Fact] public void Inactive_or_cross_scope_method_is_rejected()
    { var method = Method(); method.Deactivate(Now); Assert.Throws<DomainException>(() => Payment.Create(method.TenantId, method.EstablishmentId, Guid.NewGuid(), method, new Money(10), null, Now)); Assert.Throws<DomainException>(() => Payment.Create(Guid.NewGuid(), method.EstablishmentId, Guid.NewGuid(), Method(), new Money(10), null, Now)); }
    [Fact] public void Cash_calculates_change_and_non_cash_rejects_received_amount()
    { var cash = PaymentMethod.Create(Guid.NewGuid(), Guid.NewGuid(), "cash", "Dinheiro", false, true, Now); var payment = Payment.Create(cash.TenantId, cash.EstablishmentId, Guid.NewGuid(), cash, new Money(30), new Money(50), Now); Assert.Equal(20m, payment.Change.Amount); var pix = Method(); Assert.Throws<DomainException>(() => Payment.Create(pix.TenantId, pix.EstablishmentId, Guid.NewGuid(), pix, new Money(30), new Money(50), Now)); }
    [Fact] public void Financial_transitions_are_explicit_and_terminal()
    { var method = Method(); var confirmed = Payment.Create(method.TenantId, method.EstablishmentId, Guid.NewGuid(), method, new Money(10), null, Now); confirmed.Confirm(" tx-1 ", Now); Assert.Equal(PaymentStatus.Confirmed, confirmed.Status); Assert.Equal("tx-1", confirmed.ExternalId); Assert.Throws<DomainException>(() => confirmed.Confirm(null, Now)); var failed = Payment.Create(method.TenantId, method.EstablishmentId, Guid.NewGuid(), method, new Money(10), null, Now); failed.Fail(Now); Assert.Throws<DomainException>(() => failed.Cancel(Now)); }
    private static PaymentMethod Method(string code = "pix") => PaymentMethod.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"), code, "Pix", true, false, Now);
}
