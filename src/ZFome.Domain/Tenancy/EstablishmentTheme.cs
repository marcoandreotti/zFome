using System.Text.RegularExpressions;
using ZFome.Domain.Exceptions;

namespace ZFome.Domain.Tenancy;

/// <summary>
/// Representa o tema visual de um estabelecimento, incluindo cores primárias e secundárias, cor de fundo, cor do texto, família de fontes e URLs para logotipo e favicon.
/// </summary>
public sealed partial record EstablishmentTheme
{
    public const string DefaultPrimaryColor = "#1976D2";
    public const string DefaultSecondaryColor = "#26A69A";
    public const string DefaultBackgroundColor = "#FFFFFF";
    public const string DefaultTextColor = "#1D1D1D";
    public const string DefaultFontFamily = "Roboto";

    public string PrimaryColor { get; }
    public string SecondaryColor { get; }
    public string BackgroundColor { get; }
    public string TextColor { get; }
    public string FontFamily { get; }
    public string? LogoUrl { get; }
    public string? FaviconUrl { get; }

    public EstablishmentTheme(
        string? primaryColor = null,
        string? secondaryColor = null,
        string? backgroundColor = null,
        string? textColor = null,
        string? fontFamily = null,
        string? logoUrl = null,
        string? faviconUrl = null)
    {
        PrimaryColor = ValidateColor(primaryColor ?? DefaultPrimaryColor);
        SecondaryColor = ValidateColor(secondaryColor ?? DefaultSecondaryColor);
        BackgroundColor = ValidateColor(backgroundColor ?? DefaultBackgroundColor);
        TextColor = ValidateColor(textColor ?? DefaultTextColor);
        FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? DefaultFontFamily : fontFamily.Trim();
        LogoUrl = NormalizeUrl(logoUrl);
        FaviconUrl = NormalizeUrl(faviconUrl);
    }

    private static string ValidateColor(string color)
    {
        var normalized = color.Trim().ToUpperInvariant();
        if (!HexColor().IsMatch(normalized))
        {
            throw new DomainException("Theme colors must use #RRGGBB format.");
        }

        return normalized;
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var normalized = url.Trim();
        if (normalized.Length > 500 || !Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            throw new DomainException("Theme asset URL must be an absolute URL with at most 500 characters.");
        }

        return normalized;
    }

    [GeneratedRegex("^#[0-9A-F]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}