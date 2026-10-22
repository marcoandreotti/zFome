using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence.Write;
using ZFome.Domain.Catalog;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ZFome.Domain.Customers;
using ZFome.Domain.Ordering;
using ZFome.Domain.Promotions;
using ZFome.Domain.Payments;

namespace ZFome.Integration.Tests;

public sealed class ModelConventionsTests
{
    [Fact]
    public void Tenancy_metadata_uses_expected_schema_columns_and_delete_behavior()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata")
            .Options;
        using var context = new ZFomeDbContext(options);
        var model = context.Model;

        var establishment = model.FindEntityType(typeof(Establishment))!;
        var table = StoreObjectIdentifier.Table("establishment", "tenancy");

        Assert.Equal("tenancy", establishment.GetSchema());
        Assert.Equal("tenant_id", establishment.FindProperty(nameof(Establishment.TenantId))!.GetColumnName(table));
        Assert.Equal("timestamp with time zone", establishment.FindProperty(nameof(Establishment.CreatedAt))!.GetColumnType());
        Assert.Contains(establishment.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_establishment_slug");
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(establishment.GetForeignKeys()).DeleteBehavior);
    }

    [Fact]
    public void Catalog_metadata_has_scoped_keys_constraints_and_restrictive_links()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata").Options;
        using var context = new ZFomeDbContext(options); var model = context.Model;
        var product = model.FindEntityType(typeof(Product))!; var category = model.FindEntityType(typeof(Category))!;
        Assert.Equal("catalog", product.GetSchema());
        Assert.Contains(product.GetIndexes(), index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual([nameof(Product.TenantId), nameof(Product.EstablishmentId), nameof(Product.Code)]));
        Assert.Contains(product.GetForeignKeys(), key => key.DeleteBehavior == DeleteBehavior.Restrict && key.PrincipalEntityType.ClrType == typeof(Category));
        var designCategory = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Category))!;
        Assert.Contains(designCategory.GetCheckConstraints(), check => check.Name == "ck_category_order");
    }

    [Fact]
    public void Customer_metadata_has_scoped_contact_and_single_primary_constraints()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata").Options;
        using var context = new ZFomeDbContext(options);
        var customer = context.Model.FindEntityType(typeof(Customer))!;
        var address = context.Model.FindEntityType(typeof(CustomerAddress))!;

        Assert.Equal("customers", customer.GetSchema());
        Assert.Contains(customer.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Customer.TenantId), nameof(Customer.EstablishmentId), nameof(Customer.NormalizedPhone)]));
        Assert.True(customer.FindProperty(nameof(Customer.Version))!.IsConcurrencyToken);
        Assert.Contains(address.GetIndexes(), index => index.IsUnique && index.GetFilter() == "is_primary");
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(address.GetForeignKeys()).DeleteBehavior);
    }

    [Fact]
    public void Ordering_metadata_has_scoped_numbers_snapshots_and_concurrency()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata").Options;
        using var context = new ZFomeDbContext(options);
        var order = context.Model.FindEntityType(typeof(Order))!;
        var item = context.Model.FindEntityType(typeof(OrderItem))!;
        var history = context.Model.FindEntityType(typeof(OrderStatusHistory))!;

        Assert.Equal("orders", order.GetSchema());
        Assert.True(order.FindProperty(nameof(Order.Version))!.IsConcurrencyToken);
        Assert.Contains(order.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Order.TenantId), nameof(Order.EstablishmentId), nameof(Order.Number)]));
        Assert.Contains(order.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Order.PublicReference)]));
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(item.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Order)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(history.GetForeignKeys()).DeleteBehavior);
    }

    [Fact]
    public void Coupon_metadata_has_scoped_code_usage_and_concurrency_constraints()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata").Options;
        using var context = new ZFomeDbContext(options); var coupon = context.Model.FindEntityType(typeof(Coupon))!; var use = context.Model.FindEntityType(typeof(CouponUse))!;
        Assert.Equal("promotions", coupon.GetSchema()); Assert.True(coupon.FindProperty(nameof(Coupon.Version))!.IsConcurrencyToken);
        Assert.Contains(coupon.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Coupon.TenantId), nameof(Coupon.EstablishmentId), nameof(Coupon.Code)]));
        Assert.Contains(use.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(CouponUse.TenantId), nameof(CouponUse.EstablishmentId), nameof(CouponUse.CouponId), nameof(CouponUse.OrderId)]));
    }

    [Fact]
    public void Payment_metadata_has_scoped_codes_idempotency_and_concurrency()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql("Host=localhost;Database=metadata;Username=metadata;Password=metadata").Options;
        using var context = new ZFomeDbContext(options); var method = context.Model.FindEntityType(typeof(PaymentMethod))!; var payment = context.Model.FindEntityType(typeof(Payment))!; var idempotency = context.Model.FindEntityType(typeof(PaymentIdempotency))!;
        Assert.Contains(method.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual([nameof(PaymentMethod.TenantId), nameof(PaymentMethod.EstablishmentId), nameof(PaymentMethod.Code)]));
        Assert.True(payment.FindProperty(nameof(Payment.Version))!.IsConcurrencyToken);
        Assert.Contains(idempotency.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual([nameof(PaymentIdempotency.TenantId), nameof(PaymentIdempotency.EstablishmentId), nameof(PaymentIdempotency.Key)]));
    }
}
