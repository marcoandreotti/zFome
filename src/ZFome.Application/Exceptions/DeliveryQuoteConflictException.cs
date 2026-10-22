namespace ZFome.Application.Exceptions;

public sealed class DeliveryQuoteConflictException(string message, decimal currentFee, decimal currentTotal) : ConflictException(message)
{
    public decimal CurrentFee { get; } = currentFee;
    public decimal CurrentTotal { get; } = currentTotal;
}
