using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Promotions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade Coupon, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupon", DatabaseSchemas.Promotions, table =>
        {
            table.HasCheckConstraint("ck_coupon_value", "value > 0 and (discount_type <> 'Percentage' or value <= 100)");
            table.HasCheckConstraint("ck_coupon_window", "starts_at < ends_at");
            table.HasCheckConstraint("ck_coupon_uses", "used_count >= 0 and (maximum_uses is null or (maximum_uses > 0 and used_count <= maximum_uses))");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(40); builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(300); builder.Property(x => x.DiscountType).HasColumnName("discount_type").HasConversion<string>().HasMaxLength(20); builder.Property(x => x.Value).HasColumnName("value").HasPrecision(18, 2); builder.Property(x => x.MinimumOrder).HasColumnName("minimum_order").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x));
        builder.Property(x => x.StartsAt).HasColumnName("starts_at"); builder.Property(x => x.EndsAt).HasColumnName("ends_at"); builder.Property(x => x.MaximumUses).HasColumnName("maximum_uses"); builder.Property(x => x.UsedCount).HasColumnName("used_count"); builder.Property(x => x.IsActive).HasColumnName("is_active"); builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.Property(x => x.CreatedAt).HasColumnName("created_at"); builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id }); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Code }).IsUnique();
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Uses).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.CouponId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Classe de configuração para a entidade CouponUse, definindo mapeamentos e restrições de banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class CouponUseConfiguration : IEntityTypeConfiguration<CouponUse>
{
    public void Configure(EntityTypeBuilder<CouponUse> builder)
    {
        builder.ToTable("coupon_use", DatabaseSchemas.Promotions, table => table.HasCheckConstraint("ck_coupon_use_discount", "discount >= 0")); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.CouponId).HasColumnName("coupon_id"); builder.Property(x => x.OrderId).HasColumnName("order_id"); builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(40); builder.Property(x => x.Discount).HasColumnName("discount").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x)); builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.CouponId, x.OrderId }).IsUnique();
        builder.HasOne<ZFome.Domain.Ordering.Order>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}