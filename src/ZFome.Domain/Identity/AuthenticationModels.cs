using ZFome.Domain.Exceptions;

namespace ZFome.Domain.Identity;

public enum AuthenticationIdentityType : short
{
    AdministrativeUser = 1,
    PlatformUser = 2
}

public sealed class PlatformUser
{
    private PlatformUser() { }

    private PlatformUser(Guid id, Email email, string passwordHash, bool passwordChangeRequired, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Password hash is required.");
        Id = id;
        Email = email;
        NormalizedEmail = email.NormalizedValue;
        PasswordHash = passwordHash;
        PasswordChangeRequired = passwordChangeRequired;
        IsActive = true;
        CreatedAt = UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool PasswordChangeRequired { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PlatformUser Bootstrap(Email email, string passwordHash, DateTimeOffset now) =>
        new(Guid.NewGuid(), email, passwordHash, true, now);

    public static PlatformUser Create(Email email, string passwordHash, DateTimeOffset now) =>
        new(Guid.NewGuid(), email, passwordHash, true, now);

    public void ChangePassword(string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Password hash is required.");
        PasswordHash = passwordHash;
        PasswordChangeRequired = false;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now) { IsActive = false; UpdatedAt = now; }
    public void Activate(DateTimeOffset now) { IsActive = true; UpdatedAt = now; }
}

public sealed class AuthenticationChallenge
{
    private AuthenticationChallenge() { }
    private AuthenticationChallenge(Guid id, AuthenticationIdentityType type, Guid identityId, Guid? tenantId, string codeHash, string originHash, DateTimeOffset now, DateTimeOffset expiresAt)
    { Id = id; IdentityType = type; IdentityId = identityId; TenantId = tenantId; CodeHash = codeHash; OriginHash = originHash; CreatedAt = now; ExpiresAt = expiresAt; }

    public Guid Id { get; private set; }
    public AuthenticationIdentityType IdentityType { get; private set; }
    public Guid IdentityId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public string OriginHash { get; private set; } = string.Empty;
    public int FailedAttempts { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static AuthenticationChallenge Create(AuthenticationIdentityType type, Guid identityId, Guid? tenantId, string codeHash, string originHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.NewGuid(), type, identityId, tenantId, codeHash, originHash, now, now.Add(lifetime));

    public bool CanAttempt(DateTimeOffset now, int maximumAttempts) => ConsumedAt is null && now < ExpiresAt && FailedAttempts < maximumAttempts;
    public bool CanBeReplacedAt(DateTimeOffset now, TimeSpan resendInterval) => now >= CreatedAt.Add(resendInterval);
    public void Reject(DateTimeOffset now, int maximumAttempts) { FailedAttempts++; if (FailedAttempts >= maximumAttempts) ConsumedAt = now; }
    public void Consume(DateTimeOffset now) { if (ConsumedAt is not null) throw new DomainException("Challenge was already consumed."); ConsumedAt = now; }
}

public sealed class AdministrativeSession
{
    private AdministrativeSession() { }
    private AdministrativeSession(Guid id, Guid familyId, AuthenticationIdentityType type, Guid identityId, Guid? tenantId, string accessHash, string refreshHash, string csrfHash, bool restricted, DateTimeOffset now, DateTimeOffset accessExpiresAt, DateTimeOffset refreshExpiresAt)
    { Id = id; FamilyId = familyId; IdentityType = type; IdentityId = identityId; TenantId = tenantId; AccessTokenHash = accessHash; RefreshTokenHash = refreshHash; CsrfTokenHash = csrfHash; PasswordChangeRequired = restricted; CreatedAt = now; AccessExpiresAt = accessExpiresAt; RefreshExpiresAt = refreshExpiresAt; }

    public Guid Id { get; private set; }
    public Guid FamilyId { get; private set; }
    public AuthenticationIdentityType IdentityType { get; private set; }
    public Guid IdentityId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string AccessTokenHash { get; private set; } = string.Empty;
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public string CsrfTokenHash { get; private set; } = string.Empty;
    public bool PasswordChangeRequired { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset AccessExpiresAt { get; private set; }
    public DateTimeOffset RefreshExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static AdministrativeSession Create(Guid familyId, AuthenticationIdentityType type, Guid identityId, Guid? tenantId, string accessHash, string refreshHash, string csrfHash, bool restricted, DateTimeOffset now, TimeSpan accessLifetime, TimeSpan refreshLifetime) =>
        new(Guid.NewGuid(), familyId, type, identityId, tenantId, accessHash, refreshHash, csrfHash, restricted, now, now.Add(accessLifetime), now.Add(refreshLifetime));
    public bool IsAccessValid(DateTimeOffset now) => RevokedAt is null && now < AccessExpiresAt;
    public bool IsRefreshValid(DateTimeOffset now) => RevokedAt is null && now < RefreshExpiresAt;
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
