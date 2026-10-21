using ZFome.Domain.Exceptions;

namespace ZFome.Domain.SharedKernel;

/// <summary>
/// Representa uma quantia monetária, garantindo que seja maior ou igual a zero e com precisão de até duas casas decimais.
/// </summary>
public readonly record struct Money
{
    public static Money Zero => new(0m);

    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0m)
        {
            throw new DomainException("Money cannot be negative.");
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right)
    {
        if (right.Amount > left.Amount)
        {
            throw new DomainException("Money subtraction cannot produce a negative value.");
        }

        return new Money(left.Amount - right.Amount);
    }

    public static Money operator *(Money money, Quantity quantity) => new(money.Amount * quantity.Value);
}