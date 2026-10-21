using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Catalog;

/// <summary>
/// Representa um produto dentro do escopo de um único estabelecimento, podendo ter variações, imagens e grupos de adicionais.
/// </summary>
public sealed class Product : IEstablishmentScopedEntity
{
    private readonly List<ProductImage> images = [];
    private readonly List<ProductVariation> variations = [];
    private readonly List<ProductAdditionalGroup> additionalGroups = [];

    private Product()
    { }

    private Product(Guid tenantId, Guid establishmentId, Guid categoryId, string code, string name, Money basePrice)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || categoryId == Guid.Empty) throw new DomainException("Product scope and category are required.");
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; CategoryId = categoryId;
        Code = Normalize(code, 50, "Product code").ToUpperInvariant(); Name = Normalize(name, 150, "Product name"); BasePrice = basePrice; IsActive = true; AllowsNotes = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money BasePrice { get; private set; }
    public bool IsFeatured { get; private set; }
    public bool IsActive { get; private set; }
    public bool AllowsNotes { get; private set; }
    public IReadOnlyCollection<ProductImage> Images => images;
    public IReadOnlyCollection<ProductVariation> Variations => variations;
    public IReadOnlyCollection<ProductAdditionalGroup> AdditionalGroups => additionalGroups;

    /// <summary>Cria um produto na categoria informada, exigindo que ambos pertençam ao mesmo estabelecimento.</summary>
    public static Product Create(Guid tenantId, Guid establishmentId, Category category, string code, string name, Money basePrice)
    { if (category.TenantId != tenantId || category.EstablishmentId != establishmentId) throw new DomainException("Product category must belong to the same establishment."); return new Product(tenantId, establishmentId, category.Id, code, name, basePrice); }

    /// <summary>Atualiza os dados principais do produto e valida o escopo da categoria.</summary>
    public void Update(Category category, string code, string name, string? description, Money basePrice, bool isFeatured, bool allowsNotes)
    {
        if (category.TenantId != TenantId || category.EstablishmentId != EstablishmentId) throw new DomainException("Product category must belong to the same establishment.");
        CategoryId = category.Id; Code = Normalize(code, 50, "Product code").ToUpperInvariant(); Name = Normalize(name, 150, "Product name");
        Description = string.IsNullOrWhiteSpace(description) ? null : Normalize(description, 1000, "Product description");
        BasePrice = basePrice; IsFeatured = isFeatured; AllowsNotes = allowsNotes;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Adiciona uma imagem e mantém no máximo uma imagem principal no produto.</summary>
    public ProductImage AddImage(string url, int order, bool principal)
    {
        if (principal) foreach (var image in images) image.RemovePrincipal();
        var result = new ProductImage(Id, url, order, principal); images.Add(result); return result;
    }

    public ProductVariation AddVariation(string name, Money price, int order)
    { var result = new ProductVariation(Id, name, price, order); variations.Add(result); return result; }

    public void RemoveImage(Guid imageId)
    { if (!images.RemoveAll(image => image.Id == imageId).Equals(1)) throw new DomainException("Product image was not found."); }

    public void RemoveVariation(Guid variationId)
    { if (!variations.RemoveAll(variation => variation.Id == variationId).Equals(1)) throw new DomainException("Product variation was not found."); }

    /// <summary>Substitui integralmente as imagens, reaplicando suas invariantes.</summary>
    public void ReplaceImages(IEnumerable<(string Url, int Order, bool IsPrincipal)> replacements)
    { images.Clear(); foreach (var item in replacements) AddImage(item.Url, item.Order, item.IsPrincipal); }

    /// <summary>Substitui integralmente as variações e seus estados de ativação.</summary>
    public void ReplaceVariations(IEnumerable<(string Name, Money Price, int Order, bool IsActive)> replacements)
    { variations.Clear(); foreach (var item in replacements) { var variation = AddVariation(item.Name, item.Price, item.Order); if (!item.IsActive) variation.Deactivate(); } }

    /// <summary>Vincula um grupo de adicionais do mesmo estabelecimento, sem duplicá-lo.</summary>
    public void LinkAdditionalGroup(AdditionalGroup group, int order)
    { if (group.TenantId != TenantId || group.EstablishmentId != EstablishmentId || order < 0) throw new DomainException("Additional group must belong to the same establishment."); if (additionalGroups.All(item => item.GroupId != group.Id)) additionalGroups.Add(new ProductAdditionalGroup(TenantId, EstablishmentId, Id, group.Id, order)); }

    public void UnlinkAdditionalGroup(Guid groupId) => additionalGroups.RemoveAll(item => item.GroupId == groupId);

    /// <summary>Substitui os grupos de adicionais garantindo o isolamento por estabelecimento.</summary>
    public void ReplaceAdditionalGroups(IEnumerable<(AdditionalGroup Group, int Order)> replacements)
    {
        var values = replacements.ToArray();
        var groupIds = values.Select(x => x.Group.Id).ToHashSet();
        foreach (var (group, order) in values)
            if (group.TenantId != TenantId || group.EstablishmentId != EstablishmentId || order < 0)
                throw new DomainException("Additional group must belong to the same establishment.");
        if (values.Select(x => x.Group.Id).Distinct().Count() != values.Length)
            throw new DomainException("Product cannot link the same additional group more than once.");
        if (values.Count(x => IsCompositionPricing(x.Group.PricingStrategy)) > 1)
            throw new DomainException("A product can have only one composition pricing group.");
        if (values.SelectMany(x => x.Group.CompatibilityRules).Any(rule => !groupIds.Contains(rule.TargetGroupId) && rule.Kind == ModifierCompatibilityKind.Requires))
            throw new DomainException("Product is missing a group required by a modifier compatibility rule.");

        additionalGroups.Clear();
        foreach (var item in values) LinkAdditionalGroup(item.Group, item.Order);
    }

    private static bool IsCompositionPricing(ModifierPricingStrategy strategy) =>
        strategy is ModifierPricingStrategy.HighestPrice or ModifierPricingStrategy.Proportional;

    private static string Normalize(string input, int max, string field)
    { var value = input.Trim(); if (value.Length is < 1 || value.Length > max) throw new DomainException($"{field} is invalid."); return value; }
}

public sealed class ProductAdditionalGroup
{
    private ProductAdditionalGroup()
    { }

    internal ProductAdditionalGroup(Guid tenantId, Guid establishmentId, Guid productId, Guid groupId, int order)
    { TenantId = tenantId; EstablishmentId = establishmentId; ProductId = productId; GroupId = groupId; Order = order; }

    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid GroupId { get; private set; }
    public int Order { get; private set; }
}

public sealed class ProductImage
{
    private ProductImage()
    { }

    internal ProductImage(Guid productId, string url, int order, bool principal)
    { if (!Uri.TryCreate(url, UriKind.Absolute, out _) || url.Length > 500 || order < 0) throw new DomainException("Product image is invalid."); Id = Guid.NewGuid(); ProductId = productId; Url = url; Order = order; IsPrincipal = principal; }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public bool IsPrincipal { get; private set; }

    internal void RemovePrincipal() => IsPrincipal = false;

    public void Update(string url, int order, bool principal)
    { if (!Uri.TryCreate(url, UriKind.Absolute, out _) || url.Length > 500 || order < 0) throw new DomainException("Product image is invalid."); Url = url; Order = order; IsPrincipal = principal; }
}

public sealed class ProductVariation
{
    private ProductVariation()
    { }

    internal ProductVariation(Guid productId, string name, Money price, int order)
    { var value = name.Trim(); if (value.Length is < 1 or > 100 || order < 0) throw new DomainException("Product variation is invalid."); Id = Guid.NewGuid(); ProductId = productId; Name = value; Price = price; Order = order; IsActive = true; }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money Price { get; private set; }
    public int Order { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string name, Money price, int order)
    { var value = name.Trim(); if (value.Length is < 1 or > 100 || order < 0) throw new DomainException("Product variation is invalid."); Name = value; Price = price; Order = order; }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
