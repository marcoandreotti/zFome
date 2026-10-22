using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Identity;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Identity;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class AuthenticationCorrectionPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private DbContextOptions<ZFomeDbContext> Options => new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await using var db = new ZFomeDbContext(Options);
        await db.Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Context_isolates_tenants_and_observes_revoked_associations_and_inactive_tenant()
    {
        await using var db = new ZFomeDbContext(Options);
        var now = DateTimeOffset.UtcNow;
        var first = Tenant.Create("First", now);
        var second = Tenant.Create("Second", now);
        var own = Establishment.Create(first.Id, "Own", new Slug("own"), now);
        var foreign = Establishment.Create(second.Id, "Foreign", new Slug("foreign"), now);
        var user = AdministrativeUser.Create(first.Id, "Owner", new Email("owner@example.test"), "hash", AdministrativeRole.Owner, now);
        user.GrantEstablishmentAccess(own.Id, first.Id, now);
        db.AddRange(first, second, own, foreign, user);
        await db.SaveChangesAsync();
        var gateway = new AuthenticationContextGateway(new NpgsqlReadConnectionFactory(Microsoft.Extensions.Options.Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })));
        var identity = new AuthenticatedIdentity(Guid.NewGuid(), AuthenticationIdentityType.AdministrativeUser, user.Id, first.Id, [AdministrativeRole.Owner], [own.Id, foreign.Id], false);
        var units = await gateway.GetEstablishmentsAsync(identity, CancellationToken.None);
        Assert.Equal(own.Id, Assert.Single(units).Id);
        user.RevokeEstablishmentAccess(own.Id, now);
        await db.SaveChangesAsync();
        Assert.Empty(await gateway.GetEstablishmentsAsync(identity, CancellationToken.None));
        var repository = new AuthenticationRepository(db);
        Assert.NotNull(await repository.GetEligibleAdministrativeUserAsync(first.Id, user.Id, CancellationToken.None));
        first.Deactivate(now);
        await db.SaveChangesAsync();
        Assert.Null(await repository.GetEligibleAdministrativeUserAsync(first.Id, user.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_resends_create_one_challenge_and_later_resend_consumes_previous()
    {
        var now = DateTimeOffset.UtcNow;
        var identityId = Guid.NewGuid();
        async Task<bool> IssueAsync(DateTimeOffset time)
        {
            await using var db = new ZFomeDbContext(Options);
            return await new AuthenticationRepository(db).ReplaceChallengeAsync(
                AuthenticationChallenge.Create(AuthenticationIdentityType.PlatformUser, identityId, null, "hash", "origin", time, TimeSpan.FromMinutes(10)),
                TimeSpan.FromMinutes(1), CancellationToken.None);
        }
        var results = await Task.WhenAll(IssueAsync(now), IssueAsync(now));
        Assert.Single(results, result => result);
        Assert.True(await IssueAsync(now.AddMinutes(2)));
        await using var verification = new ZFomeDbContext(Options);
        var challenges = await verification.AuthenticationChallenges.Where(item => item.IdentityId == identityId).ToArrayAsync();
        Assert.Equal(2, challenges.Length);
        Assert.Single(challenges, item => item.ConsumedAt == null);
    }

    [Fact]
    public async Task Identity_revocation_is_persisted_across_all_families()
    {
        await using var db = new ZFomeDbContext(Options);
        var now = DateTimeOffset.UtcNow;
        var identityId = Guid.NewGuid();
        foreach (var index in new[] { 1, 2 }) db.AdministrativeSessions.Add(AdministrativeSession.Create(Guid.NewGuid(), AuthenticationIdentityType.PlatformUser,
            identityId, null, $"access-{index}", $"refresh-{index}", "csrf", true, now, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)));
        var challenge = AuthenticationChallenge.Create(AuthenticationIdentityType.PlatformUser, identityId, null, "code", "origin", now, TimeSpan.FromMinutes(5));
        db.AuthenticationChallenges.Add(challenge);
        await db.SaveChangesAsync();
        challenge.Consume(now);
        var repository = new AuthenticationRepository(db);
        await repository.RevokeIdentitySessionsAsync(AuthenticationIdentityType.PlatformUser, identityId, now, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
        Assert.All(await db.AdministrativeSessions.Where(item => item.IdentityId == identityId).ToArrayAsync(), item => Assert.NotNull(item.RevokedAt));
    }
}
