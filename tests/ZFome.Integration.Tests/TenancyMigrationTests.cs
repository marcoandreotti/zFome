using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using ZFome.Infrastructure.Identity;
using ZFome.Infrastructure.Migrations;
using ZFome.Infrastructure.Persistence.Write;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class TenancyMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Onboarding_migration_preserves_existing_configuration_on_upgrade_rollback_and_reapply()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260903132436_AdministrativeAuthentication");
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        var tenant = Guid.NewGuid(); var unit = Guid.NewGuid(); var table = Guid.NewGuid();
        await connection.ExecuteAsync("""
            insert into tenancy.tenant(id,name,is_active,created_at,updated_at,public_code) values(@Tenant,'Existing',true,now(),now(),'migration-onboarding');
            insert into tenancy.establishment(id,tenant_id,trade_name,slug,is_active,created_at,updated_at) values(@Unit,@Tenant,'Existing','migration-onboarding',true,now(),now());
            insert into operations.service_table(id,tenant_id,establishment_id,code,qr_code_token,is_active) values(@Table,@Tenant,@Unit,'A1','existing-opaque-token',true);
            """, new { Tenant = tenant, Unit = unit, Table = table });
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Null(await connection.ExecuteScalarAsync<DateTimeOffset?>("select onboarding_completed_at from tenancy.establishment where id=@Unit", new { Unit = unit }));
        await connection.ExecuteAsync("update tenancy.establishment set onboarding_completed_at=now() where id=@Unit", new { Unit = unit });
        await migrator.MigrateAsync("20260903132436_AdministrativeAuthentication");
        Assert.Equal("existing-opaque-token", await connection.ExecuteScalarAsync<string>("select qr_code_token from operations.service_table where id=@Table", new { Table = table }));
        await context.Database.MigrateAsync();
        Assert.Equal("Existing", await connection.ExecuteScalarAsync<string>("select trade_name from tenancy.establishment where id=@Unit", new { Unit = unit }));
        Assert.Equal(2, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.columns where (table_schema='tenancy' and table_name='establishment' and column_name='onboarding_completed_at') or (table_schema='operations' and table_name='service_table' and column_name='creation_intent')"));
    }

    [Fact]
    public async Task Initial_tenancy_migration_applies_and_rolls_back_on_empty_database()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString(), npgsql =>
                npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName))
            .Options;
        await using var context = new ZFomeDbContext(options);

        await context.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        var tablesAfterUp = await connection.QueryAsync<string>(
            "select table_name from information_schema.tables where table_schema = 'tenancy';");
        Assert.Equal(
            ["establishment", "establishment_theme", "platform_provisioning_intent", "tenant"],
            tablesAfterUp.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(6, await connection.ExecuteScalarAsync<int>("select count(*) from identity.administrative_role;"));
        Assert.Equal(5, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='operations';"));

        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase);
        var tablesAfterDown = await connection.QueryAsync<string>(
            "select table_name from information_schema.tables where table_schema = 'tenancy';");
        Assert.Empty(tablesAfterDown);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema in ('identity','operations');"));
    }


    [Fact]
    public async Task Product_catalog_migration_upgrades_rolls_back_and_reapplies()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options); var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260928204645_DeliveryManagement"); await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        var expected = new[] { "additional", "additional_group", "additional_group_item", "category", "offer_unavailability", "product", "product_additional_group", "product_image", "product_variation" };
        Assert.Equal(expected, (await connection.QueryAsync<string>("select table_name from information_schema.tables where table_schema='catalog' order by table_name;")).ToArray());
        await migrator.MigrateAsync("20260820120132_IdentityOperations");
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='catalog';"));
        await migrator.MigrateAsync("20260928204645_DeliveryManagement");
        Assert.Equal(expected.Length, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='catalog';"));
    }

    [Fact]
    public async Task Product_modifiers_migration_backfills_existing_groups_as_additive_without_changing_prices()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName))
            .Options;
        await using var context = new ZFomeDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260928204645_DeliveryManagement");

        var tenantId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var additionalId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await connection.ExecuteAsync("""
            insert into tenancy.tenant(id, name, is_active, created_at, updated_at)
            values (@TenantId, 'Migration tenant', true, now(), now());
            insert into tenancy.establishment(id, tenant_id, trade_name, slug, is_active, created_at, updated_at)
            values (@EstablishmentId, @TenantId, 'Migration unit', @Slug, true, now(), now());
            insert into catalog.category(id, tenant_id, establishment_id, name, "order", is_active)
            values (@CategoryId, @TenantId, @EstablishmentId, 'Pizzas', 0, true);
            insert into catalog.product(id, tenant_id, establishment_id, category_id, code, name, base_price, is_featured, is_active, allows_notes)
            values (@ProductId, @TenantId, @EstablishmentId, @CategoryId, 'PIZZA-1', 'Pizza', 50, false, true, true);
            insert into catalog.additional(id, tenant_id, establishment_id, name, price, is_active)
            values (@AdditionalId, @TenantId, @EstablishmentId, 'Borda', 8, true);
            insert into catalog.additional_group(id, tenant_id, establishment_id, name, minimum_selection, maximum_selection, is_active)
            values (@GroupId, @TenantId, @EstablishmentId, 'Bordas', 0, 1, true);
            insert into catalog.additional_group_item(group_id, additional_id, tenant_id, establishment_id, "order")
            values (@GroupId, @AdditionalId, @TenantId, @EstablishmentId, 0);
            insert into catalog.product_additional_group(product_id, group_id, tenant_id, establishment_id, "order")
            values (@ProductId, @GroupId, @TenantId, @EstablishmentId, 0);
            """, new { TenantId = tenantId, EstablishmentId = establishmentId, CategoryId = categoryId, ProductId = productId, GroupId = groupId, AdditionalId = additionalId, Slug = $"migration-{Guid.NewGuid():N}" });

        const string legacyPriceSql = "select p.base_price + a.price from catalog.product p join catalog.product_additional_group pg on pg.tenant_id=p.tenant_id and pg.establishment_id=p.establishment_id and pg.product_id=p.id join catalog.additional_group_item gi on gi.tenant_id=pg.tenant_id and gi.establishment_id=pg.establishment_id and gi.group_id=pg.group_id join catalog.additional a on a.tenant_id=gi.tenant_id and a.establishment_id=gi.establishment_id and a.id=gi.additional_id where p.tenant_id=@TenantId and p.establishment_id=@EstablishmentId and p.id=@ProductId";
        var priceBefore = await connection.ExecuteScalarAsync<decimal>(legacyPriceSql, new { TenantId = tenantId, EstablishmentId = establishmentId, ProductId = productId });
        Assert.Equal(58m, priceBefore);

        await context.Database.MigrateAsync();
        Assert.Equal("Additive", await connection.ExecuteScalarAsync<string>("select pricing_strategy from catalog.additional_group where id=@GroupId", new { GroupId = groupId }));
        Assert.Equal(priceBefore, await connection.ExecuteScalarAsync<decimal>(legacyPriceSql, new { TenantId = tenantId, EstablishmentId = establishmentId, ProductId = productId }));
        Assert.Equal(50m, await connection.ExecuteScalarAsync<decimal>("select base_price from catalog.product where id=@ProductId", new { ProductId = productId }));
        Assert.Equal(8m, await connection.ExecuteScalarAsync<decimal>("select price from catalog.additional where id=@AdditionalId", new { AdditionalId = additionalId }));

        await migrator.MigrateAsync("20260928204645_DeliveryManagement");
        Assert.Equal(priceBefore, await connection.ExecuteScalarAsync<decimal>(legacyPriceSql, new { TenantId = tenantId, EstablishmentId = establishmentId, ProductId = productId }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.columns where table_schema='catalog' and table_name='additional_group' and column_name='pricing_strategy'"));
        await context.Database.MigrateAsync();
        Assert.Equal("Additive", await connection.ExecuteScalarAsync<string>("select pricing_strategy from catalog.additional_group where id=@GroupId", new { GroupId = groupId }));
    }


    [Fact]
    public async Task Customer_records_migration_upgrades_rolls_back_and_reapplies()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await context.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();

        Assert.Equal(["customer", "customer_address"], (await connection.QueryAsync<string>("select table_name from information_schema.tables where table_schema='customers' order by table_name;")).ToArray());
        await migrator.MigrateAsync("20260820135644_ProductCatalog");
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='customers';"));
        await context.Database.MigrateAsync();
        Assert.Equal(2, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='customers';"));
    }


    [Fact]
    public async Task Order_lifecycle_migration_upgrades_rolls_back_and_reapplies()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options); var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260903001324_PublicOrderingApi"); await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        var expected = new[] { "order", "order_item", "order_item_additional", "order_number_counter", "order_status_history", "public_order_request" };
        Assert.Equal(expected, (await connection.QueryAsync<string>("select table_name from information_schema.tables where table_schema='orders' order by table_name;")).ToArray());
        await migrator.MigrateAsync("20260902201903_CustomerRecords");
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='orders';"));
        await migrator.MigrateAsync("20260928234032_ModifierOrderSnapshots");
        Assert.Equal(expected.Length + 2, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='orders';"));
    }


    [Fact]
    public async Task Coupon_management_migration_upgrades_rolls_back_and_reapplies()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options); var migrator = context.Database.GetService<IMigrator>(); await context.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        Assert.Equal(["coupon", "coupon_use"], (await connection.QueryAsync<string>("select table_name from information_schema.tables where table_schema='promotions' order by table_name;")).ToArray());
        await migrator.MigrateAsync("20260902220405_OrderLifecycle"); Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='promotions';"));
        await context.Database.MigrateAsync(); Assert.Equal(2, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='promotions';"));
    }


    [Fact]
    public async Task Order_payments_migration_upgrades_rolls_back_and_reapplies()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options); var migrator = context.Database.GetService<IMigrator>(); await context.Database.MigrateAsync(); await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        Assert.Equal(["payment", "payment_idempotency", "payment_method"], (await connection.QueryAsync<string>("select table_name from information_schema.tables where table_schema='payments' order by table_name;")).ToArray());
        await migrator.MigrateAsync("20260902231354_CouponManagement"); Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='payments';")); await context.Database.MigrateAsync(); Assert.Equal(3, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='payments';"));
    }


    [Fact]
    public async Task Public_ordering_migration_upgrades_rolls_back_and_reapplies()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options); var migrator = context.Database.GetService<IMigrator>(); await context.Database.MigrateAsync(); await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='orders' and table_name='public_order_request';"));
        await migrator.MigrateAsync("20260902235922_OrderPayments");
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='orders' and table_name='public_order_request';"));
        await context.Database.MigrateAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='orders' and table_name='public_order_request';"));
    }

    [Fact]
    public async Task Order_scheduling_migration_upgrades_rolls_back_and_reapplies()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await context.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='operations' and table_name='order_scheduling_policy';"));
        Assert.Equal(2, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.columns where table_schema='orders' and table_name='order' and column_name in ('scheduled_at_utc','scheduled_time_zone_id');"));

        await migrator.MigrateAsync("20260903001324_PublicOrderingApi");
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='operations' and table_name='order_scheduling_policy';"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.columns where table_schema='orders' and table_name='order' and column_name in ('scheduled_at_utc','scheduled_time_zone_id');"));
        await context.Database.MigrateAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='operations' and table_name='order_scheduling_policy';"));
    }


    [Fact]
    public async Task Administrative_authentication_migration_upgrades_rolls_back_and_reapplies()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await context.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        var expected = new[] { "administrative_session", "authentication_challenge", "platform_user" };
        Assert.Equal(expected, (await connection.QueryAsync<string>("select table_name from information_schema.tables where table_schema='identity' and table_name in ('administrative_session','authentication_challenge','platform_user') order by table_name;")).ToArray());
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.columns where table_schema='tenancy' and table_name='tenant' and column_name='public_code';"));
        await migrator.MigrateAsync("20260903001324_PublicOrderingApi");
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='identity' and table_name in ('administrative_session','authentication_challenge','platform_user');"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.columns where table_schema='tenancy' and table_name='tenant' and column_name='public_code';"));
        await context.Database.MigrateAsync();
        Assert.Equal(expected.Length, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where table_schema='identity' and table_name in ('administrative_session','authentication_challenge','platform_user');"));
    }

    [Fact]
    public async Task Business_hours_availability_migration_preserves_existing_hours_and_rolls_back()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString(), npgsql => npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName)).Options;
        await using var context = new ZFomeDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260915233908_EstablishmentOnboarding");
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        var tenant = Guid.NewGuid(); var unit = Guid.NewGuid(); var hours = Guid.NewGuid();
        await connection.ExecuteAsync("""
            insert into tenancy.tenant(id,name,is_active,created_at,updated_at,public_code) values(@Tenant,'Existing',true,now(),now(),'availability-migration');
            insert into tenancy.establishment(id,tenant_id,trade_name,slug,is_active,created_at,updated_at) values(@Unit,@Tenant,'Existing','availability-migration',true,now(),now());
            insert into operations.business_hours(id,tenant_id,establishment_id,day_of_week,opens_at,closes_at,is_active) values(@Hours,@Tenant,@Unit,1,'10:00','20:00',true);
            """, new { Tenant = tenant, Unit = unit, Hours = hours });

        await context.Database.MigrateAsync();
        Assert.Equal("America/Sao_Paulo", await connection.ExecuteScalarAsync<string>("select time_zone_id from tenancy.establishment where id=@Unit", new { Unit = unit }));
        Assert.Equal(3, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.tables where (table_schema='operations' and table_name in ('service_pause','service_schedule_exception')) or (table_schema='catalog' and table_name='offer_unavailability');"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("select count(*) from operations.business_hours where id=@Hours", new { Hours = hours }));

        await migrator.MigrateAsync("20260915233908_EstablishmentOnboarding");
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("select count(*) from information_schema.columns where table_schema='tenancy' and table_name='establishment' and column_name='time_zone_id';"));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("select count(*) from operations.business_hours where id=@Hours", new { Hours = hours }));
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
    }


    [Fact]
    public async Task Concurrent_platform_bootstrap_creates_exactly_one_superuser()
    {
        var contextOptions = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString(), npgsql =>
                npgsql.MigrationsAssembly(typeof(ZFomeDbContextFactory).Assembly.FullName))
            .Options;
        await using (var migrationContext = new ZFomeDbContext(contextOptions))
        {
            await migrationContext.Database.MigrateAsync();
        }

        var bootstrapOptions = Options.Create(new PlatformBootstrapOptions
        {
            Email = "first.superuser@zfome.test",
            TemporaryPassword = "temporary-password"
        });

        async Task BootstrapAsync()
        {
            await using var context = new ZFomeDbContext(contextOptions);
            var bootstrapper = new PlatformBootstrapper(
                new AuthenticationRepository(context),
                new AspNetPasswordHasher(),
                bootstrapOptions,
                TimeProvider.System);
            await bootstrapper.InitializeAsync(CancellationToken.None);
        }

        await Task.WhenAll(BootstrapAsync(), BootstrapAsync());

        await using var verificationContext = new ZFomeDbContext(contextOptions);
        var user = Assert.Single(await verificationContext.PlatformUsers.AsNoTracking().ToListAsync());
        Assert.True(user.IsActive);
        Assert.True(user.PasswordChangeRequired);
        Assert.NotEqual("temporary-password", user.PasswordHash);

        await BootstrapAsync();
        Assert.Equal(1, await verificationContext.PlatformUsers.CountAsync());
    }
}
