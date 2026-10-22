using Microsoft.EntityFrameworkCore;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence.Write;
using ZFome.Infrastructure.Persistence.Write.Repositories;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class PublicOrderingPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database=new PostgreSqlBuilder("postgres:17-alpine").Build();
    public Task InitializeAsync()=>database.StartAsync();
    public async Task DisposeAsync()=>await database.DisposeAsync();

    [Fact]
    public async Task Idempotency_key_is_scoped_by_establishment()
    {
        var options=Options(); var now=DateTimeOffset.UtcNow;
        await using var context=new ZFomeDbContext(options); await context.Database.EnsureCreatedAsync();
        var firstTenant=Tenant.Create("First",now); var secondTenant=Tenant.Create("Second",now);
        var firstUnit=Establishment.Create(firstTenant.Id,"First",new Slug("first"),now); var secondUnit=Establishment.Create(secondTenant.Id,"Second",new Slug("second"),now);
        var firstOrder=Confirmed(firstTenant.Id,firstUnit.Id,now,1); var secondOrder=Confirmed(secondTenant.Id,secondUnit.Id,now,1);
        context.AddRange(firstTenant,secondTenant,firstUnit,secondUnit,firstOrder,secondOrder); await context.SaveChangesAsync();
        var repository=new PublicOrderRequestRepository(context);
        await repository.AddAsync(PublicOrderRequest.Create(firstTenant.Id,firstUnit.Id,"same-key-123",new string('a',64),firstOrder.Id,now),CancellationToken.None);
        await repository.AddAsync(PublicOrderRequest.Create(secondTenant.Id,secondUnit.Id,"same-key-123",new string('b',64),secondOrder.Id,now),CancellationToken.None);
        Assert.Equal(2,await context.PublicOrderRequests.CountAsync());
    }

    [Fact]
    public async Task Failed_public_confirmation_rolls_back_saved_order()
    {
        var options=Options(); var now=DateTimeOffset.UtcNow; Guid tenantId; Guid unitId;
        await using(var setup=new ZFomeDbContext(options)){await setup.Database.EnsureCreatedAsync();var tenant=Tenant.Create("Group",now);var unit=Establishment.Create(tenant.Id,"Unit",new Slug("unit"),now);setup.AddRange(tenant,unit);await setup.SaveChangesAsync();tenantId=tenant.Id;unitId=unit.Id;}
        await using(var context=new ZFomeDbContext(options))
        {
            var transaction=new PublicOrderTransaction(context); var repository=new OrderRepository(context);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>transaction.ExecuteAsync<int>(async token=>{var order=Confirmed(tenantId,unitId,now,1);await repository.AddAsync(order,token);throw new InvalidOperationException("simulated failure");},CancellationToken.None));
        }
        await using var verification=new ZFomeDbContext(options); Assert.Empty(await verification.Orders.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_scheduled_orders_cannot_exceed_the_last_slot_capacity()
    {
        var options = Options(); var now = DateTimeOffset.UtcNow; Guid tenantId; Guid unitId;
        await using (var setup = new ZFomeDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var tenant = Tenant.Create("Group", now);
            var unit = Establishment.Create(tenant.Id, "Unit", new Slug("unit"), now);
            setup.AddRange(tenant, unit);
            await setup.SaveChangesAsync();
            tenantId = tenant.Id; unitId = unit.Id;
        }

        var slot = DateTimeOffset.FromUnixTimeSeconds(now.AddHours(2).ToUnixTimeSeconds() / 60 * 60);
        var bothRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = 0;
        async Task ConfirmAsync(long number)
        {
            await using var context = new ZFomeDbContext(options);
            var transaction = new PublicOrderTransaction(context);
            var repository = new OrderRepository(context);
            await transaction.ExecuteAsync(async token =>
            {
                var reservations = await context.Orders.CountAsync(x => x.TenantId == tenantId
                    && x.EstablishmentId == unitId && x.ScheduledAtUtc == slot
                    && x.Status != OrderStatus.Cancelled && x.Status != OrderStatus.Rejected, token);
                Assert.Equal(0, reservations);
                if (Interlocked.Increment(ref ready) == 2) bothRead.TrySetResult(true);
                await bothRead.Task.WaitAsync(token);
                var order = Order.Create(tenantId, unitId, OrderServiceType.Pickup, null, null, null, null, null, now);
                order.AddItem(Guid.NewGuid(), null, "Product", null, new Money(10), new Quantity(1), [], null, now);
                order.Schedule(slot, "UTC", now);
                order.Confirm(number, now);
                await repository.AddAsync(order, token);
                return true;
            }, CancellationToken.None);
        }

        var outcomes = await Task.WhenAll(
            CaptureConflictAsync(() => ConfirmAsync(1)),
            CaptureConflictAsync(() => ConfirmAsync(2)));
        Assert.Single(outcomes, x => !x);
        Assert.Single(outcomes, x => x);
        await using var verification = new ZFomeDbContext(options);
        Assert.Equal(1, await verification.Orders.CountAsync(x => x.TenantId == tenantId
            && x.EstablishmentId == unitId && x.ScheduledAtUtc == slot));
    }

    private static async Task<bool> CaptureConflictAsync(Func<Task> action)
    {
        try { await action(); return false; }
        catch (ZFome.Application.Exceptions.ConflictException) { return true; }
    }

    private DbContextOptions<ZFomeDbContext> Options()=>new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    private static Order Confirmed(Guid tenantId,Guid unitId,DateTimeOffset now,long number){var order=Order.Create(tenantId,unitId,OrderServiceType.Pickup,null,null,null,null,null,now);order.AddItem(Guid.NewGuid(),null,"Product",null,new Money(10),new Quantity(1),[],null,now);order.Confirm(number,now);return order;}
}
