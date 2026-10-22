using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class PostgreSqlPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => database.StartAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Ef_write_and_dapper_read_connect_and_keep_tenants_isolated()
    {
        var cancellationToken = CancellationToken.None;
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
        await using var writeContext = new ZFomeDbContext(options);

        Assert.True(await writeContext.Database.CanConnectAsync(cancellationToken));

        var factory = new NpgsqlReadConnectionFactory(Options.Create(new DatabaseOptions
        {
            ConnectionString = database.GetConnectionString()
        }));
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            create temporary table tenant_probe (
                tenant_id uuid not null,
                value text not null
            );
            """,
            cancellationToken: cancellationToken));

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            "insert into tenant_probe (tenant_id, value) values (@TenantA, 'A'), (@TenantB, 'B');",
            new { TenantA = tenantA, TenantB = tenantB },
            cancellationToken: cancellationToken));

        var gateway = new TenantProbeReadGateway(connection);
        Assert.Equal(["A"], await gateway.ListAsync(tenantA, cancellationToken));
        Assert.Equal(["B"], await gateway.ListAsync(tenantB, cancellationToken));
    }

    private sealed class TenantProbeReadGateway(DbConnection connection)
    {
        public async Task<IReadOnlyList<string>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var values = await connection.QueryAsync<string>(new CommandDefinition(
                "select value from tenant_probe where tenant_id = @TenantId order by value;",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
            return values.AsList();
        }
    }
}
