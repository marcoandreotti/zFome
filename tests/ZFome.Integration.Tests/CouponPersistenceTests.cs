using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZFome.Application.Exceptions;
using ZFome.Domain.Ordering;
using ZFome.Domain.Promotions;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using ZFome.Infrastructure.Persistence.Write.Repositories;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class CouponPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public Task InitializeAsync() => database.StartAsync(); public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Code_is_unique_per_establishment_and_Dapper_is_tenant_scoped()
    {
        var options = CreateOptions(); Guid tenantId; Guid firstId; Guid secondId; var now = DateTimeOffset.UtcNow;
        await using (var context = new ZFomeDbContext(options))
        {
            await context.Database.EnsureCreatedAsync(); var tenant = Tenant.Create("Group", now); var first = Establishment.Create(tenant.Id, "First", new Slug("first"), now); var second = Establishment.Create(tenant.Id, "Second", new Slug("second"), now); context.AddRange(tenant, first, second); await context.SaveChangesAsync();
            tenantId = tenant.Id; firstId = first.Id; secondId = second.Id;
            context.Coupons.AddRange(CreateCoupon(tenant.Id, first.Id, "SAVE", now, 2), CreateCoupon(tenant.Id, second.Id, "SAVE", now, 2)); await context.SaveChangesAsync();
            await Assert.ThrowsAsync<ConflictException>(() => new CouponRepository(context).AddAsync(CreateCoupon(tenant.Id, first.Id, "save", now, 2), CancellationToken.None));
        }
        var gateway = new CouponReadGateway(new NpgsqlReadConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })));
        Assert.Single(await gateway.ListAsync(tenantId, firstId, CancellationToken.None)); Assert.Single(await gateway.ListAsync(tenantId, secondId, CancellationToken.None)); Assert.Empty(await gateway.ListAsync(Guid.NewGuid(), firstId, CancellationToken.None));
        var page=await gateway.SearchAsync(tenantId,firstId,"sav",true,1,20,CancellationToken.None);Assert.Equal(1,page.Total);Assert.Equal("SAVE",Assert.Single(page.Items).Code);
        Assert.Empty((await gateway.SearchAsync(Guid.NewGuid(),firstId,null,null,1,20,CancellationToken.None)).Items);
    }

    [Fact]
    public async Task Only_one_concurrent_confirmation_consumes_last_use_and_loser_remains_draft()
    {
        var options = CreateOptions(); var now = DateTimeOffset.UtcNow; Guid tenantId; Guid unitId; Guid couponId; Guid firstOrderId; Guid secondOrderId;
        await using (var setup = new ZFomeDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync(); var tenant = Tenant.Create("Group", now); var unit = Establishment.Create(tenant.Id, "Unit", new Slug("unit"), now); var coupon = CreateCoupon(tenant.Id, unit.Id, "LAST", now, 1);
            setup.AddRange(tenant, unit, coupon); await setup.SaveChangesAsync(); var evaluation = coupon.Evaluate(new Money(20), now);
            var first = Draft(tenant.Id, unit.Id, coupon, evaluation, now); var second = Draft(tenant.Id, unit.Id, coupon, evaluation, now); setup.Orders.AddRange(first, second); await setup.SaveChangesAsync();
            tenantId = tenant.Id; unitId = unit.Id; couponId = coupon.Id; firstOrderId = first.Id; secondOrderId = second.Id;
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var ready = 0;
        async Task<bool> Confirm(Guid orderId)
        {
            await using var context = new ZFomeDbContext(options); var coupon = await context.Coupons.Include(x => x.Uses).SingleAsync(x => x.Id == couponId); var order = await context.Orders.Include(x => x.Items).ThenInclude(x => x.Additionals).Include(x => x.History).SingleAsync(x => x.Id == orderId);
            if (Interlocked.Increment(ref ready) == 2) gate.SetResult(); await gate.Task;
            await using var transaction = await context.Database.BeginTransactionAsync();
            try { coupon.Consume(order.Id, order.Subtotal, now); var number = await new OrderNumberSequence(context).ReserveAsync(tenantId, unitId, CancellationToken.None); order.Confirm(number, now); await new OrderRepository(context).SaveChangesAsync(CancellationToken.None); await transaction.CommitAsync(); return true; }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Confirm(firstOrderId), Confirm(secondOrderId)); Assert.Single(results, value => value);
        await using var verification = new ZFomeDbContext(options); var persistedCoupon = await verification.Coupons.Include(x => x.Uses).SingleAsync(x => x.Id == couponId); var orders = await verification.Orders.Where(x => x.Id == firstOrderId || x.Id == secondOrderId).ToArrayAsync();
        Assert.Equal(1, persistedCoupon.UsedCount); Assert.Single(persistedCoupon.Uses); Assert.Single(orders, x => x.Status == OrderStatus.Confirmed); Assert.Single(orders, x => x.Status == OrderStatus.Draft);
    }

    private static Coupon CreateCoupon(Guid tenantId, Guid establishmentId, string code, DateTimeOffset now, int limit) => Coupon.Create(tenantId, establishmentId, code, null, CouponDiscountType.FixedAmount, 5, Money.Zero, now.AddDays(-1), now.AddDays(1), limit, now);
    private static Order Draft(Guid tenantId, Guid establishmentId, Coupon coupon, CouponEvaluation evaluation, DateTimeOffset now) { var order = Order.Create(tenantId, establishmentId, OrderServiceType.Pickup, null, null, null, null, null, now); order.AddItem(Guid.NewGuid(), null, "Produto", null, new Money(20), new Quantity(1), [], null, now); order.ApplyCoupon(coupon.Id, coupon.Code, evaluation.Discount, now); return order; }
    private DbContextOptions<ZFomeDbContext> CreateOptions() => new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
}
