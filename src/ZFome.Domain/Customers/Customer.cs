using System.Net.Mail;
using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Customers;

/// <summary>
/// Representa um cliente dentro do escopo de um único estabelecimento.
/// </summary>
public sealed class Customer : IEstablishmentScopedEntity
{
    private readonly List<CustomerAddress> addresses = [];

    private Customer()
    {
    }

    private Customer(Guid tenantId, Guid establishmentId, string name, string phone, string? email, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty)
        {
            throw new DomainException("Customer scope is required.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        SetContact(name, phone, email);
        Version = Guid.NewGuid();
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string NormalizedPhone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? NormalizedEmail { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<CustomerAddress> Addresses => addresses;

    /// <summary>Cria um cliente dentro do escopo de um único estabelecimento.</summary>
    public static Customer Create(
        Guid tenantId,
        Guid establishmentId,
        string name,
        string phone,
        string? email,
        DateTimeOffset now) =>
        new(tenantId, establishmentId, name, phone, email, now);

    /// <summary>Atualiza e normaliza os dados de contato do cliente.</summary>
    public void UpdateContact(string name, string phone, string? email, DateTimeOffset now)
    {
        SetContact(name, phone, email);
        Touch(now);
    }

    /// <summary>Adiciona um endereço e mantém no máximo um endereço principal.</summary>
    public CustomerAddress AddAddress(
        string label,
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        bool isPrimary,
        DateTimeOffset now)
    {
        if (isPrimary)
        {
            ClearPrimaryAddress();
        }

        var address = new CustomerAddress(
            TenantId,
            EstablishmentId,
            Id,
            label,
            street,
            number,
            complement,
            neighborhood,
            city,
            state,
            postalCode,
            isPrimary);
        addresses.Add(address);
        Touch(now);
        return address;
    }

    /// <summary>Atualiza um endereço pertencente ao cliente e troca o principal de forma consistente.</summary>
    public void UpdateAddress(
        Guid addressId,
        string label,
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        bool isPrimary,
        DateTimeOffset now)
    {
        var address = GetAddress(addressId);
        if (isPrimary)
        {
            ClearPrimaryAddress();
        }

        address.Update(label, street, number, complement, neighborhood, city, state, postalCode, isPrimary);
        Touch(now);
    }

    /// <summary>Remove um endereço pertencente ao cliente.</summary>
    public void RemoveAddress(Guid addressId, DateTimeOffset now)
    {
        var address = GetAddress(addressId);
        addresses.Remove(address);
        Touch(now);
    }

    private CustomerAddress GetAddress(Guid addressId) =>
        addresses.SingleOrDefault(item => item.Id == addressId)
        ?? throw new DomainException("Customer address was not found.");

    private void ClearPrimaryAddress()
    {
        foreach (var address in addresses.Where(item => item.IsPrimary))
        {
            address.RemovePrimary();
        }
    }

    private void SetContact(string name, string phone, string? email)
    {
        Name = NormalizeRequired(name, 150, "Customer name");
        Phone = NormalizeRequired(phone, 30, "Customer phone");
        NormalizedPhone = NormalizePhone(Phone);
        Email = NormalizeEmail(email);
        NormalizedEmail = Email?.ToUpperInvariant();
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }

    /// <summary>Produz a representação canônica usada para comparar telefones.</summary>
    public static string NormalizePhone(string phone)
    {
        var normalized = new string(phone.Where(char.IsDigit).ToArray());
        // Remove only known Brazilian prefixes followed by a valid DDD and
        // a 10/11 digit national number. This avoids mistaking DDD 55 or an
        // unrelated 14 digit number for a country-code-prefixed phone.
        if (normalized.StartsWith("550", StringComparison.Ordinal)
            && IsBrazilianNationalNumber(normalized[3..]))
        {
            normalized = normalized[3..];
        }
        else if (normalized.StartsWith("55", StringComparison.Ordinal)
            && IsBrazilianNationalNumber(normalized[2..]))
        {
            normalized = normalized[2..];
        }
        else if (normalized.StartsWith('0') && IsBrazilianNationalNumber(normalized[1..]))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length is < 8 or > 15)
        {
            throw new DomainException("Customer phone is invalid.");
        }

        return normalized;
    }

    /// <summary>Retorna formas históricas equivalentes para localizar registros legados sem alterá-los.</summary>
    public static string[] GetPhoneLookupCandidates(string normalizedPhone)
    {
        var canonical = NormalizePhone(normalizedPhone);
        if (canonical.Length is not (10 or 11))
        {
            return [canonical];
        }

        return [canonical, $"55{canonical}", $"0{canonical}", $"550{canonical}"];
    }

    private static bool IsBrazilianNationalNumber(string value) =>
        value.Length is 10 or 11
        && BrazilianAreaCodes.Contains(value[..2], StringComparer.Ordinal);

    private static readonly string[] BrazilianAreaCodes =
    [
        "11", "12", "13", "14", "15", "16", "17", "18", "19",
        "21", "22", "24", "27", "28",
        "31", "32", "33", "34", "35", "37", "38",
        "41", "42", "43", "44", "45", "46", "47", "48", "49",
        "51", "53", "54", "55",
        "61", "62", "63", "64", "65", "66", "67", "68", "69",
        "71", "73", "74", "75", "77", "79",
        "81", "82", "83", "84", "85", "86", "87", "88", "89",
        "91", "92", "93", "94", "95", "96", "97", "98", "99"
    ];

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > 254 || !MailAddress.TryCreate(normalized, out var parsed) || parsed.Address != normalized)
        {
            throw new DomainException("Customer email is invalid.");
        }

        return normalized;
    }

    internal static string NormalizeRequired(string input, int maximumLength, string field)
    {
        var normalized = input.Trim();
        if (normalized.Length is < 1 || normalized.Length > maximumLength)
        {
            throw new DomainException($"{field} is invalid.");
        }

        return normalized;
    }
}

public sealed class CustomerAddress : IEstablishmentScopedEntity
{
    private CustomerAddress()
    {
    }

    internal CustomerAddress(
        Guid tenantId,
        Guid establishmentId,
        Guid customerId,
        string label,
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        bool isPrimary)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        CustomerId = customerId;
        Update(label, street, number, complement, neighborhood, city, state, postalCode, isPrimary);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Street { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string? Complement { get; private set; }
    public string Neighborhood { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }

    internal void Update(
        string label,
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        bool isPrimary)
    {
        Label = Customer.NormalizeRequired(label, 50, "Address label");
        Street = Customer.NormalizeRequired(street, 200, "Address street");
        Number = Customer.NormalizeRequired(number, 30, "Address number");
        Complement = string.IsNullOrWhiteSpace(complement) ? null : Customer.NormalizeRequired(complement, 100, "Address complement");
        Neighborhood = Customer.NormalizeRequired(neighborhood, 100, "Address neighborhood");
        City = Customer.NormalizeRequired(city, 100, "Address city");
        State = Customer.NormalizeRequired(state, 2, "Address state").ToUpperInvariant();
        PostalCode = NormalizePostalCode(postalCode);
        IsPrimary = isPrimary;
    }

    internal void RemovePrimary() => IsPrimary = false;

    private static string NormalizePostalCode(string postalCode)
    {
        var normalized = new string(postalCode.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length is < 5 or > 12)
        {
            throw new DomainException("Address postal code is invalid.");
        }

        return normalized;
    }
}
