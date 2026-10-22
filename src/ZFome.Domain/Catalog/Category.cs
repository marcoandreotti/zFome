using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Catalog;

/// <summary>
/// Representa uma categoria de produtos dentro do escopo de um único estabelecimento, podendo ter subcategorias e produtos associados.
/// </summary>
public sealed class Category : IEstablishmentScopedEntity
{
    private Category()
    { }

    private Category(Guid tenantId, Guid establishmentId, string name, int order)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty) throw new DomainException("Tenant and establishment are required.");
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; SetName(name); SetOrder(order); IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Order { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Cria uma categoria ativa no catálogo do estabelecimento informado.</summary>
    public static Category Create(Guid tenantId, Guid establishmentId, string name, int order = 0) => new(tenantId, establishmentId, name, order);

    /// <summary>Atualiza os dados exibidos da categoria preservando suas invariantes.</summary>
    public void Update(string name, string? description, int order, string? imageUrl)
    {
        SetName(name);
        SetOrder(order);
        Description = NormalizeOptional(description, 500, "Category description");
        ImageUrl = NormalizeUrl(imageUrl);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Altera a categoria pai garantindo isolamento de estabelecimento e ausência de ciclos.</summary>
    public void ChangeParent(Guid? parentId, Guid parentTenantId, Guid parentEstablishmentId, IReadOnlySet<Guid> parentAncestorIds)
    {
        if (parentId is null) { ParentCategoryId = null; return; }
        if (parentTenantId != TenantId || parentEstablishmentId != EstablishmentId) throw new DomainException("Parent category must belong to the same establishment.");
        if (parentId == Id || parentAncestorIds.Contains(Id)) throw new DomainException("Category hierarchy cannot contain cycles.");
        ParentCategoryId = parentId;
    }

    private void SetName(string name)
    { var value = name.Trim(); if (value.Length is < 1 or > 150) throw new DomainException("Category name must contain 1 to 150 characters."); Name = value; }

    private void SetOrder(int order)
    { if (order < 0) throw new DomainException("Category order cannot be negative."); Order = order; }

    private static string? NormalizeOptional(string? input, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var value = input.Trim();
        if (value.Length > maximumLength) throw new DomainException($"{field} is invalid.");
        return value;
    }

    private static string? NormalizeUrl(string? input)
    {
        var value = NormalizeOptional(input, 500, "Category image URL");
        if (value is not null && !Uri.TryCreate(value, UriKind.Absolute, out _)) throw new DomainException("Category image URL is invalid.");
        return value;
    }
}