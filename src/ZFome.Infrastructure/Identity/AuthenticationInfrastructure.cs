using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using ZFome.Application.Abstractions.Identity;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Infrastructure.Identity;

public sealed class AuthenticationSecretProtector : IAuthenticationSecretProtector
{
    public string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    public string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    public string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public bool Verify(string hash, string value) => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hash), Convert.FromHexString(Hash(value)));
}

public sealed class AuthenticationEmailOptions
{
    public const string SectionName = "AuthenticationEmail";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}

public sealed class SmtpAuthenticationCodeSender(IOptions<AuthenticationEmailOptions> options, ILogger<SmtpAuthenticationCodeSender> logger) : IAuthenticationCodeSender
{
    public async Task SendAsync(string email, string code, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.Host) || string.IsNullOrWhiteSpace(value.FromAddress)) throw new InvalidOperationException("Authentication e-mail delivery is not configured.");
        using var message = new MailMessage(value.FromAddress, email, "Código de acesso ZFome", $"Seu código de acesso é {code}. Ele expira em {expiresAt:O}.");
        using var client = new SmtpClient(value.Host, value.Port) { EnableSsl = value.EnableSsl };
        if (!string.IsNullOrWhiteSpace(value.Username)) client.Credentials = new NetworkCredential(value.Username, value.Password);
        logger.LogInformation("Sending authentication code to e-mail domain {EmailDomain}", email[(email.LastIndexOf('@') + 1)..]);
        await client.SendMailAsync(message, ct);
    }
}

public sealed class AuthenticationRepository(ZFomeDbContext db) : IAuthenticationRepository
{
    public Task<AdministrativeUser?> GetEligibleAdministrativeUserAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        db.AdministrativeUsers.SingleOrDefaultAsync(user => user.Id == userId && user.TenantId == tenantId && user.IsActive &&
            db.Tenants.Any(tenant => tenant.Id == tenantId && tenant.IsActive), ct);

    public async Task RevokeIdentitySessionsAsync(AuthenticationIdentityType type, Guid identityId, DateTimeOffset now, CancellationToken ct)
    {
        var sessions = await db.AdministrativeSessions.Where(session => session.IdentityType == type && session.IdentityId == identityId && session.RevokedAt == null).ToListAsync(ct);
        foreach (var session in sessions) session.Revoke(now);
        // Desafios anteriores também não podem abrir uma nova sessão após a troca de senha.
        var challenges = await db.AuthenticationChallenges.Where(challenge => challenge.IdentityType == type && challenge.IdentityId == identityId && challenge.ConsumedAt == null).ToListAsync(ct);
        foreach (var challenge in challenges.Where(item => item.ConsumedAt is null)) challenge.Consume(now);
    }

    public async Task<bool> ReplaceChallengeAsync(AuthenticationChallenge challenge, TimeSpan resendInterval, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serializa reenvios da mesma identidade entre instâncias da API.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({challenge.IdentityType.ToString() + challenge.IdentityId}, 0))", ct);
        var previous = await db.AuthenticationChallenges.Where(item => item.IdentityType == challenge.IdentityType && item.IdentityId == challenge.IdentityId).ToListAsync(ct);
        if (previous.Any(item => !item.CanBeReplacedAt(challenge.CreatedAt, resendInterval))) return false;
        foreach (var item in previous.Where(item => item.ConsumedAt == null)) item.Consume(challenge.CreatedAt);
        await db.AuthenticationChallenges.AddAsync(challenge, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
    public async Task<(Guid TenantId, AdministrativeUser User)?> FindAdministrativeUserAsync(string tenantCode, string normalizedEmail, CancellationToken ct)
    { var code = Tenant.NormalizePublicCode(tenantCode); var user = await db.AdministrativeUsers.Include(x => x.RoleMemberships).Include(x => x.EstablishmentAccesses).SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail && db.Tenants.Any(t => t.Id == x.TenantId && t.PublicCode == code && t.IsActive), ct); return user is null ? null : (user.TenantId, user); }
    public Task<PlatformUser?> FindPlatformUserAsync(string email, CancellationToken ct) => db.PlatformUsers.SingleOrDefaultAsync(x => x.NormalizedEmail == email, ct);
    public Task<PlatformUser?> GetPlatformUserAsync(Guid id, CancellationToken ct) => db.PlatformUsers.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> AnyPlatformUserAsync(CancellationToken ct) => db.PlatformUsers.AnyAsync(ct);
    public Task<int> CountActivePlatformUsersAsync(CancellationToken ct) => db.PlatformUsers.CountAsync(x => x.IsActive, ct);
    public async Task AddPlatformUserAsync(PlatformUser user, CancellationToken ct) => await db.PlatformUsers.AddAsync(user, ct);
    public Task<AuthenticationChallenge?> GetChallengeAsync(Guid id, CancellationToken ct) => db.AuthenticationChallenges.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddChallengeAsync(AuthenticationChallenge challenge, CancellationToken ct) => await db.AuthenticationChallenges.AddAsync(challenge, ct);
    public Task<int> CountRecentChallengesAsync(string originHash, DateTimeOffset since, CancellationToken ct) => db.AuthenticationChallenges.CountAsync(x => x.OriginHash == originHash && x.CreatedAt >= since, ct);
    public Task<AdministrativeSession?> FindSessionByAccessHashAsync(string hash, CancellationToken ct) => db.AdministrativeSessions.SingleOrDefaultAsync(x => x.AccessTokenHash == hash, ct);
    public Task<AdministrativeSession?> FindSessionByRefreshHashAsync(string hash, CancellationToken ct) => db.AdministrativeSessions.SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, ct);
    public async Task AddSessionAsync(AdministrativeSession session, CancellationToken ct) => await db.AdministrativeSessions.AddAsync(session, ct);
    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct) { var sessions = await db.AdministrativeSessions.Where(x => x.FamilyId == familyId && x.RevokedAt == null).ToListAsync(ct); foreach (var s in sessions) s.Revoke(now); }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class AuthenticationSessionResolver(ZFomeDbContext db, IAuthenticationSecretProtector secrets, TimeProvider clock) : IAuthenticationSessionResolver
{
    public async Task<AuthenticatedIdentity?> ResolveAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = secrets.Hash(token); var s = await db.AdministrativeSessions.AsNoTracking().SingleOrDefaultAsync(x => x.AccessTokenHash == hash, ct); if (s is null || !s.IsAccessValid(clock.GetUtcNow())) return null;
        if (s.IdentityType == AuthenticationIdentityType.AdministrativeUser && !await db.Tenants.AnyAsync(tenant => tenant.Id == s.TenantId && tenant.IsActive, ct)) return null;
        if (s.IdentityType == AuthenticationIdentityType.PlatformUser) { var p = await db.PlatformUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == s.IdentityId && x.IsActive, ct); return p is null ? null : new(s.Id, s.IdentityType, s.IdentityId, null, [], [], p.PasswordChangeRequired); }
        var u = await db.AdministrativeUsers.AsNoTracking().Include(x => x.RoleMemberships).Include(x => x.EstablishmentAccesses).SingleOrDefaultAsync(x => x.Id == s.IdentityId && x.TenantId == s.TenantId && x.IsActive, ct); return u is null ? null : new(s.Id, s.IdentityType, s.IdentityId, s.TenantId, u.RoleMemberships.Select(x => x.Role).ToArray(), u.EstablishmentAccesses.Where(x => x.IsActive).Select(x => x.EstablishmentId).ToArray(), false);
    }
}
