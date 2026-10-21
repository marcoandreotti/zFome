using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class IdentityPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Access_gateway_observes_explicit_active_same_tenant_association()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var context = new ZFomeDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var tenantA = Tenant.Create("Tenant A", now);
        var tenantB = Tenant.Create("Tenant B", now);
        var unitA = Establishment.Create(tenantA.Id, "Unit A", new Slug("identity-unit-a"), now);
        var unitB = Establishment.Create(tenantB.Id, "Unit B", new Slug("identity-unit-b"), now);
        context.AddRange(tenantA, tenantB, unitA, unitB);
        var user = AdministrativeUser.Create(
            tenantA.Id, "Marco", new Email("marco@example.com"), "hash", AdministrativeRole.Owner, now);
        user.GrantEstablishmentAccess(unitA.Id, tenantA.Id, now);
        context.AdministrativeUsers.Add(user);
        await context.SaveChangesAsync();
        Assert.Equal(6, await context.Database.SqlQueryRaw<int>("select count(*)::int as \"Value\" from identity.administrative_role").SingleAsync());

        var gateway = new EstablishmentAccessGateway(new NpgsqlReadConnectionFactory(Options.Create(new DatabaseOptions
        { ConnectionString = database.GetConnectionString() })));
        Assert.True(await gateway.HasActiveAccessAsync(tenantA.Id, user.Id, unitA.Id, CancellationToken.None));
        Assert.False(await gateway.HasActiveAccessAsync(tenantA.Id, user.Id, unitB.Id, CancellationToken.None));

        user.RevokeEstablishmentAccess(unitA.Id, now.AddMinutes(1));
        await context.SaveChangesAsync();
        Assert.False(await gateway.HasActiveAccessAsync(tenantA.Id, user.Id, unitA.Id, CancellationToken.None));

        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "insert into identity.user_establishment_access(user_id, tenant_id, establishment_id, is_active, created_at) values (@UserId,@TenantId,@EstablishmentId,true,@Now)",
            new { UserId = user.Id, TenantId = tenantB.Id, EstablishmentId = unitB.Id, Now = now }));
    }
}
