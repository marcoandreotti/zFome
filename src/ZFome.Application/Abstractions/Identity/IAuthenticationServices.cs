using ZFome.Domain.Identity;

namespace ZFome.Application.Abstractions.Identity;

public interface IAuthenticationSecretProtector
{
    string GenerateCode();
    string GenerateToken();
    string Hash(string value);
    bool Verify(string hash, string value);
}

public interface IAuthenticationCodeSender
{
    Task SendAsync(string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken);
}

public interface IAuthenticationRepository
{
    Task<AdministrativeUser?> GetEligibleAdministrativeUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task RevokeIdentitySessionsAsync(AuthenticationIdentityType type, Guid identityId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> ReplaceChallengeAsync(AuthenticationChallenge challenge, TimeSpan resendInterval, CancellationToken cancellationToken);
    Task<(Guid TenantId, AdministrativeUser User)?> FindAdministrativeUserAsync(string tenantCode, string normalizedEmail, CancellationToken cancellationToken);
    Task<PlatformUser?> FindPlatformUserAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<PlatformUser?> GetPlatformUserAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> AnyPlatformUserAsync(CancellationToken cancellationToken);
    Task<int> CountActivePlatformUsersAsync(CancellationToken cancellationToken);
    Task AddPlatformUserAsync(PlatformUser user, CancellationToken cancellationToken);
    Task<AuthenticationChallenge?> GetChallengeAsync(Guid id, CancellationToken cancellationToken);
    Task AddChallengeAsync(AuthenticationChallenge challenge, CancellationToken cancellationToken);
    Task<int> CountRecentChallengesAsync(string originHash, DateTimeOffset since, CancellationToken cancellationToken);
    Task<AdministrativeSession?> FindSessionByAccessHashAsync(string hash, CancellationToken cancellationToken);
    Task<AdministrativeSession?> FindSessionByRefreshHashAsync(string hash, CancellationToken cancellationToken);
    Task AddSessionAsync(AdministrativeSession session, CancellationToken cancellationToken);
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record AuthenticatedIdentity(
    Guid SessionId,
    AuthenticationIdentityType Type,
    Guid IdentityId,
    Guid? TenantId,
    IReadOnlyCollection<AdministrativeRole> Roles,
    IReadOnlyCollection<Guid> EstablishmentIds,
    bool PasswordChangeRequired);

public interface IAuthenticationSessionResolver
{
    Task<AuthenticatedIdentity?> ResolveAsync(string accessToken, CancellationToken cancellationToken);
}
