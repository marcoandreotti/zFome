using System.Net.Mail;
using ZFome.Domain.Exceptions;

namespace ZFome.Domain.Identity;

/// <summary>
/// Representa um endereço de e-mail válido, com valor original e valor normalizado para comparação.
/// </summary>
public sealed record Email
{
    public string Value { get; }
    public string NormalizedValue { get; }

    public Email(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 150 || !MailAddress.TryCreate(trimmed, out var parsed) || parsed.Address != trimmed)
        {
            throw new DomainException("A valid email with at most 150 characters is required.");
        }

        Value = trimmed;
        NormalizedValue = trimmed.ToUpperInvariant();
    }
}