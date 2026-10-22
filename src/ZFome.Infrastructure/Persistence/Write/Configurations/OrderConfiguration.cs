using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;

namespace ZFome.Infrastructure.Persistence.Write.Configurations;

/// <summary>
/// Classe de configuração para a entidade Order, definindo mapeamentos de propriedades, restrições e relacionamentos no banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("order", DatabaseSchemas.Orders, table =>
        {
            table.HasCheckConstraint("ck_order_number", "number is null or number > 0");
            table.HasCheckConstraint("ck_order_totals", "subtotal >= 0 and discount >= 0 and fees >= 0 and total >= 0");
            table.HasCheckConstraint("ck_order_schedule", "(scheduled_at_utc is null and scheduled_time_zone_id is null) or (scheduled_at_utc is not null and scheduled_time_zone_id is not null)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.Number).HasColumnName("number"); builder.Property(x => x.PublicReference).HasColumnName("public_reference").HasMaxLength(48);
        builder.Property(x => x.ServiceType).HasColumnName("service_type").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.CustomerId).HasColumnName("customer_id"); builder.Property(x => x.CustomerName).HasColumnName("customer_name").HasMaxLength(150); builder.Property(x => x.CustomerPhone).HasColumnName("customer_phone").HasMaxLength(30);
        builder.Property(x => x.TableId).HasColumnName("table_id");
        builder.Property(x => x.ScheduledAtUtc).HasColumnName("scheduled_at_utc");
        builder.Property(x => x.ScheduledTimeZoneId).HasColumnName("scheduled_time_zone_id").HasMaxLength(100);
        builder.Property(x => x.DeliveryRegionId).HasColumnName("delivery_region_id");
        builder.Property(x => x.DeliveryRegionName).HasColumnName("delivery_region_name").HasMaxLength(100);
        Money(builder.Property(x => x.DeliveryFee), "delivery_fee");
        builder.Property(x => x.DeliveryEstimatedMinutes).HasColumnName("delivery_estimated_minutes");
        Money(builder.Property(x => x.Subtotal), "subtotal"); Money(builder.Property(x => x.Discount), "discount"); Money(builder.Property(x => x.Fees), "fees"); Money(builder.Property(x => x.Total), "total");
        builder.Property(x => x.CouponId).HasColumnName("coupon_id"); builder.Property(x => x.CouponCode).HasColumnName("coupon_code").HasMaxLength(40);
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); builder.Property(x => x.CreatedAt).HasColumnName("created_at"); builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.OwnsOne(x => x.DeliveryAddress, address =>
        {
            address.Property(x => x.Street).HasColumnName("delivery_street").HasMaxLength(200);
            address.Property(x => x.Number).HasColumnName("delivery_number").HasMaxLength(30);
            address.Property(x => x.Complement).HasColumnName("delivery_complement").HasMaxLength(100);
            address.Property(x => x.Neighborhood).HasColumnName("delivery_neighborhood").HasMaxLength(100);
            address.Property(x => x.City).HasColumnName("delivery_city").HasMaxLength(100);
            address.Property(x => x.State).HasColumnName("delivery_state").HasMaxLength(2);
            address.Property(x => x.PostalCode).HasColumnName("delivery_postal_code").HasMaxLength(12);
        });
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.Number }).IsUnique().HasFilter("number is not null");
        builder.HasIndex(x => x.PublicReference).IsUnique().HasFilter("public_reference is not null");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.ServiceType, x.ScheduledAtUtc }).HasFilter("scheduled_at_utc is not null");
        builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ZFome.Domain.Promotions.Coupon>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.CouponId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.History).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }

    private static void Money(PropertyBuilder<Money> property, string column) => property.HasColumnName(column).HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x));
}

/// <summary>
/// Classe de configuração para a entidade OrderItem, definindo mapeamentos de propriedades, restrições e relacionamentos no banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_item", DatabaseSchemas.Orders, table => table.HasCheckConstraint("ck_order_item_values", "unit_price >= 0 and quantity > 0 and total >= 0"));
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.OrderId).HasColumnName("order_id"); builder.Property(x => x.ProductId).HasColumnName("product_id"); builder.Property(x => x.VariationId).HasColumnName("variation_id");
        builder.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(150); builder.Property(x => x.VariationName).HasColumnName("variation_name").HasMaxLength(100); builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x)); builder.Property(x => x.BasePrice).HasColumnName("base_price").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x)); builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 3).HasConversion(x => x.Value, x => new Quantity(x)); builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x));
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id }); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.OrderId });
        builder.HasMany(x => x.Additionals).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderItemId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.ModifierGroups).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderItemId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderItemModifierGroupConfiguration : IEntityTypeConfiguration<OrderItemModifierGroup>
{
    public void Configure(EntityTypeBuilder<OrderItemModifierGroup> builder)
    {
        builder.ToTable("order_item_modifier_group", DatabaseSchemas.Orders);
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.OrderItemId).HasColumnName("order_item_id"); builder.Property(x => x.ModifierGroupId).HasColumnName("modifier_group_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(x => x.PricingStrategy).HasColumnName("pricing_strategy").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x));
        builder.HasAlternateKey(x => new { x.TenantId, x.EstablishmentId, x.Id });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.OrderItemId });
        builder.HasMany(x => x.Options).WithOne().HasForeignKey(x => new { x.TenantId, x.EstablishmentId, x.OrderItemModifierGroupId }).HasPrincipalKey(x => new { x.TenantId, x.EstablishmentId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderItemModifierOptionConfiguration : IEntityTypeConfiguration<OrderItemModifierOption>
{
    public void Configure(EntityTypeBuilder<OrderItemModifierOption> builder)
    {
        builder.ToTable("order_item_modifier_option", DatabaseSchemas.Orders, table =>
        {
            table.HasCheckConstraint("ck_order_item_modifier_option_portion", "(portion_numerator is null and portion_denominator is null) or (portion_numerator > 0 and portion_denominator > 0 and portion_numerator <= portion_denominator)");
            table.HasCheckConstraint("ck_order_item_modifier_option_values", "unit_price >= 0 and quantity > 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id");
        builder.Property(x => x.OrderItemModifierGroupId).HasColumnName("order_item_modifier_group_id"); builder.Property(x => x.ModifierOptionId).HasColumnName("modifier_option_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x));
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 3).HasConversion(x => x.Value, x => new Quantity(x));
        builder.Property(x => x.PortionNumerator).HasColumnName("portion_numerator"); builder.Property(x => x.PortionDenominator).HasColumnName("portion_denominator");
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.OrderItemModifierGroupId });
    }
}

/// <summary>
/// Classe de configuração para a entidade OrderItemAdditional, definindo mapeamentos de propriedades, restrições e relacionamentos no banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class OrderItemAdditionalConfiguration : IEntityTypeConfiguration<OrderItemAdditional>
{
    public void Configure(EntityTypeBuilder<OrderItemAdditional> builder)
    {
        builder.ToTable("order_item_additional", DatabaseSchemas.Orders, table => table.HasCheckConstraint("ck_order_item_additional_values", "unit_price >= 0 and quantity > 0")); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.OrderItemId).HasColumnName("order_item_id"); builder.Property(x => x.AdditionalId).HasColumnName("additional_id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150); builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2).HasConversion(x => x.Amount, x => new Money(x)); builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 3).HasConversion(x => x.Value, x => new Quantity(x));
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.OrderItemId });
    }
}

/// <summary>
/// Classe de configuração para a entidade OrderStatusHistory, definindo mapeamentos de propriedades, restrições e relacionamentos no banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_history", DatabaseSchemas.Orders); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.OrderId).HasColumnName("order_id"); builder.Property(x => x.PreviousStatus).HasColumnName("previous_status").HasConversion<string>().HasMaxLength(30); builder.Property(x => x.NewStatus).HasColumnName("new_status").HasConversion<string>().HasMaxLength(30); builder.Property(x => x.OccurredAt).HasColumnName("occurred_at"); builder.Property(x => x.ActorId).HasColumnName("actor_id"); builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(500); builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.OrderId, x.OccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.EstablishmentId, x.NewStatus, x.OccurredAt })
            .IncludeProperties(x => x.OrderId)
            .HasDatabaseName("ix_order_status_history_reporting");
    }
}

/// <summary>
/// Classe de configuração para a entidade OrderNumberCounter, definindo mapeamentos de propriedades, restrições e relacionamentos no banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class OrderNumberCounter
{
    public Guid TenantId { get; set; }
    public Guid EstablishmentId { get; set; }
    public long LastNumber { get; set; }
}

/// <summary>
/// Classe de configuração para a entidade OrderNumberCounter, definindo mapeamentos de propriedades, restrições e relacionamentos no banco de dados usando o Entity Framework Core.
/// </summary>
internal sealed class OrderNumberCounterConfiguration : IEntityTypeConfiguration<OrderNumberCounter>
{
    public void Configure(EntityTypeBuilder<OrderNumberCounter> builder)
    { builder.ToTable("order_number_counter", DatabaseSchemas.Orders, table => table.HasCheckConstraint("ck_order_number_counter", "last_number > 0")); builder.HasKey(x => new { x.TenantId, x.EstablishmentId }); builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.EstablishmentId).HasColumnName("establishment_id"); builder.Property(x => x.LastNumber).HasColumnName("last_number"); builder.HasOne<ZFome.Domain.Tenancy.Establishment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EstablishmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict); }
}
