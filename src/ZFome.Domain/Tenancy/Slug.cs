using System.Text.RegularExpressions;
using ZFome.Domain.Exceptions;

namespace ZFome.Domain.Tenancy;

/// <summary>
/// Slug é uma representação textual de um identificador único, geralmente usado em URLs para identificar recursos de forma legível e amigável.
/// Ele é composto por letras minúsculas, números e hífens, e deve ter entre 3 e 100 caracteres de comprimento.
/// O Slug é normalizado para garantir consistência, removendo espaços e convertendo todas as letras para minúsculas.
/// </summary>
public sealed partial record Slug
{
    public string Value { get; }

    public Slug(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Replace(' ', '-');
        if (normalized.Length is < 3 or > 100 || !ValidSlug().IsMatch(normalized))
        {
            throw new DomainException("Slug must contain 3 to 100 lowercase letters, numbers, or hyphens.");
        }

        Value = normalized;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidSlug();
}