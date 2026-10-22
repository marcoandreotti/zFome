using System.Numerics;
using ZFome.Domain.Exceptions;

namespace ZFome.Domain.SharedKernel;

/// <summary>Represents an exact positive fraction of a composed product unit.</summary>
public readonly record struct ModifierPortion
{
    public int Numerator { get; }
    public int Denominator { get; }

    public ModifierPortion(int numerator, int denominator)
    {
        if (numerator <= 0 || denominator <= 0 || numerator > denominator)
            throw new DomainException("Modifier portion must be a positive fraction no greater than one.");

        var divisor = GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public static bool SumEqualsOne(IEnumerable<ModifierPortion> portions)
    {
        var numerator = BigInteger.Zero;
        var denominator = BigInteger.One;
        var count = 0;
        foreach (var portion in portions)
        {
            if (portion.Numerator <= 0 || portion.Denominator <= 0) return false;
            numerator = numerator * portion.Denominator + (BigInteger)portion.Numerator * denominator;
            denominator *= portion.Denominator;
            count++;
        }

        return count > 0 && numerator == denominator;
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0) (left, right) = (right, left % right);
        return left;
    }
}
