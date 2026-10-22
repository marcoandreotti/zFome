using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Catalog;
using ZFome.Domain.SharedKernel;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade Category, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("category", DatabaseSchemas.Catalog, table => table.HasCheckConstraint("ck_category_order", "\"order\" >= 0"));
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.ParentCategoryId).HasColumnName("parent_category_id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500); builder.Property(x => x.Order).HasColumnName("order"); builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(500); builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.ParentCategoryId, x.Order });
        builder.HasOne<Category>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.ParentCategoryId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OfferUnavailabilityConfiguration : IEntityTypeConfiguration<OfferUnavailability>
{
    public void Configure(EntityTypeBuilder<OfferUnavailability> builder)
    {
        builder.ToTable("offer_unavailability", DatabaseSchemas.Catalog, table =>
            table.HasCheckConstraint("ck_offer_unavailability_interval", "ends_at is null or ends_at > starts_at"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<short>();
        builder.Property(x => x.OfferId).HasColumnName("offer_id");
        builder.Property(x => x.StartsAt).HasColumnName("starts_at");
        builder.Property(x => x.EndsAt).HasColumnName("ends_at");
        builder.Property(x => x.ReactivatedAt).HasColumnName("reactivated_at");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(250);
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Kind, x.OfferId, x.StartsAt });
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.EstablishmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Classe de configuração para a entidade Product, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("product", DatabaseSchemas.Catalog, table => table.HasCheckConstraint("ck_product_base_price", "base_price >= 0"));
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150); builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(x => x.BasePrice).HasColumnName("base_price").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x)); builder.Property(x => x.IsFeatured).HasColumnName("is_featured"); builder.Property(x => x.IsActive).HasColumnName("is_active"); builder.Property(x => x.AllowsNotes).HasColumnName("allows_notes");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id }); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Code }).IsUnique(); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.CategoryId });
        builder.HasOne<Category>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.CategoryId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Variations).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.AdditionalGroups).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.ProductId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Classe de configuração para a entidade ProductImage, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_image", DatabaseSchemas.Catalog, table => table.HasCheckConstraint("ck_product_image_order", "\"order\" >= 0")); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.ProductId).HasColumnName("product_id"); builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(500); builder.Property(x => x.Order).HasColumnName("order"); builder.Property(x => x.IsPrincipal).HasColumnName("is_principal"); builder.HasIndex(x => new { x.ProductId, x.Order }); builder.HasIndex(x => x.ProductId).HasFilter("is_principal").IsUnique();
    }
}

/// <summary>
/// Classe de configuração para a entidade ProductVariation, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class ProductVariationConfiguration : IEntityTypeConfiguration<ProductVariation>
{
    public void Configure(EntityTypeBuilder<ProductVariation> builder)
    {
        builder.ToTable("product_variation", DatabaseSchemas.Catalog, table => { table.HasCheckConstraint("ck_product_variation_price", "price >= 0"); table.HasCheckConstraint("ck_product_variation_order", "\"order\" >= 0"); }); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.ProductId).HasColumnName("product_id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100); builder.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x)); builder.Property(x => x.Order).HasColumnName("order"); builder.Property(x => x.IsActive).HasColumnName("is_active"); builder.HasIndex(x => new { x.ProductId, x.Order });
    }
}

/// <summary>
/// Classe de configuração para a entidade Additional, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class AdditionalConfiguration : IEntityTypeConfiguration<Additional>
{
    public void Configure(EntityTypeBuilder<Additional> builder)
    {
        builder.ToTable("additional", DatabaseSchemas.Catalog, table => table.HasCheckConstraint("ck_additional_price", "price >= 0")); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150); builder.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x)); builder.Property(x => x.IsActive).HasColumnName("is_active"); builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id }); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Name }); builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Classe de configuração para a entidade AdditionalGroup, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class AdditionalGroupConfiguration : IEntityTypeConfiguration<AdditionalGroup>
{
    public void Configure(EntityTypeBuilder<AdditionalGroup> builder)
    {
        builder.ToTable("additional_group", DatabaseSchemas.Catalog, table => { table.HasCheckConstraint("ck_additional_group_minimum", "minimum_selection >= 0"); table.HasCheckConstraint("ck_additional_group_maximum", "maximum_selection >= 1"); table.HasCheckConstraint("ck_additional_group_range", "minimum_selection <= maximum_selection"); }); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150); builder.Property(x => x.MinimumSelection).HasColumnName("minimum_selection"); builder.Property(x => x.MaximumSelection).HasColumnName("maximum_selection"); builder.Property(x => x.PricingStrategy).HasColumnName("pricing_strategy").HasConversion<string>().HasMaxLength(30).HasDefaultValue(ModifierPricingStrategy.Additive); builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30).HasDefaultValue(ModifierGroupType.Additional); builder.Property(x => x.RequiresCompleteComposition).HasColumnName("requires_complete_composition").HasDefaultValue(false); builder.Property(x => x.IsActive).HasColumnName("is_active"); builder.Ignore(x => x.IsRequired); builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id }); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Name }); builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.GroupId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade); builder.HasMany(x => x.CompatibilityRules).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.SourceGroupId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade); builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Classe de configuração para a entidade AdditionalGroupItem, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class AdditionalGroupItemConfiguration : IEntityTypeConfiguration<AdditionalGroupItem>
{
    public void Configure(EntityTypeBuilder<AdditionalGroupItem> builder)
    {
        builder.ToTable("additional_group_item", DatabaseSchemas.Catalog, table => table.HasCheckConstraint("ck_additional_group_item_order", "\"order\" >= 0")); builder.HasKey(x => new { x.GroupId, x.AdditionalId }); builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.GroupId, x.AdditionalId }); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.GroupId).HasColumnName("group_id"); builder.Property(x => x.AdditionalId).HasColumnName("additional_id"); builder.Property(x => x.Order).HasColumnName("order"); builder.HasOne<Additional>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.AdditionalId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict); builder.HasIndex(x => new { x.GroupId, x.Order });
    }
}

internal sealed class AdditionalGroupCompatibilityRuleConfiguration : IEntityTypeConfiguration<AdditionalGroupCompatibilityRule>
{
    public void Configure(EntityTypeBuilder<AdditionalGroupCompatibilityRule> builder)
    {
        builder.ToTable("additional_group_compatibility_rule", DatabaseSchemas.Catalog, table => table.HasCheckConstraint("ck_additional_group_compatibility_rule_target", "source_group_id <> target_group_id or source_additional_id <> target_additional_id"));
        builder.HasKey(x => new { x.TenantId, x.EstablishmentId, x.SourceGroupId, x.SourceAdditionalId, x.TargetGroupId, x.TargetAdditionalId, x.Kind });
        builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.SourceGroupId).HasColumnName("source_group_id");
        builder.Property(x => x.SourceAdditionalId).HasColumnName("source_additional_id");
        builder.Property(x => x.TargetGroupId).HasColumnName("target_group_id");
        builder.Property(x => x.TargetAdditionalId).HasColumnName("target_additional_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20);
        builder.HasOne<AdditionalGroupItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.SourceGroupId, x.SourceAdditionalId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.GroupId, x.AdditionalId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AdditionalGroupItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.TargetGroupId, x.TargetAdditionalId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.GroupId, x.AdditionalId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.TargetGroupId, x.TargetAdditionalId });
    }
}

/// <summary>
/// Classe de configuração para a entidade ProductAdditionalGroup, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class ProductAdditionalGroupConfiguration : IEntityTypeConfiguration<ProductAdditionalGroup>
{
    public void Configure(EntityTypeBuilder<ProductAdditionalGroup> builder)
    {
        builder.ToTable("product_additional_group", DatabaseSchemas.Catalog, table => table.HasCheckConstraint("ck_product_additional_group_order", "\"order\" >= 0")); builder.HasKey(x => new { x.ProductId, x.GroupId }); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.ProductId).HasColumnName("product_id"); builder.Property(x => x.GroupId).HasColumnName("group_id"); builder.Property(x => x.Order).HasColumnName("order"); builder.HasOne<AdditionalGroup>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.GroupId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict); builder.HasIndex(x => new { x.ProductId, x.Order });
    }
}
