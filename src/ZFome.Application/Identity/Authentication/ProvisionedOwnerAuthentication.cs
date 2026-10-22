using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Exceptions;
using ZFome.Domain.Identity;

namespace ZFome.Application.Identity.Authentication;

public sealed class ProvisioningCompleteAuthenticationCommandHandler(IAuthenticationRepository repository,
    IAuthenticationSecretProtector secrets, TimeProvider clock, AuthenticationOptions options)
    : ICommandHandler<CompleteAuthenticationCommand, AuthenticationTokens>
{
    public async Task<AuthenticationTokens> HandleAsync(CompleteAuthenticationCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow(); var challenge = await repository.GetChallengeAsync(command.ChallengeId, ct);
        if (challenge is null || !challenge.CanAttempt(now, options.MaximumCodeAttempts) ||
            !secrets.Verify(challenge.CodeHash, command.Code) || !secrets.Verify(challenge.OriginHash, command.Origin))
        { if (challenge is not null && challenge.CanAttempt(now, options.MaximumCodeAttempts)) { challenge.Reject(now, options.MaximumCodeAttempts); await repository.SaveChangesAsync(ct); } throw new UnauthorizedException("Authentication could not be completed."); }
        challenge.Consume(now);
        var result = await ProvisioningAuthenticationTokenFactory.CreateAsync(repository, secrets,
            challenge.IdentityType, challenge.IdentityId, challenge.TenantId, Guid.NewGuid(), now, options, ct);
        await repository.SaveChangesAsync(ct); return result;
    }
}

public sealed class ProvisioningRefreshAuthenticationCommandHandler(IAuthenticationRepository repository,
    IAuthenticationSecretProtector secrets, TimeProvider clock, AuthenticationOptions options)
    : ICommandHandler<RefreshAuthenticationCommand, AuthenticationTokens>
{
    public async Task<AuthenticationTokens> HandleAsync(RefreshAuthenticationCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var old = await repository.FindSessionByRefreshHashAsync(secrets.Hash(command.RefreshToken), ct)
            ?? throw new UnauthorizedException("Session is invalid.");
        if (old.RevokedAt is not null) { await repository.RevokeFamilyAsync(old.FamilyId, now, ct); await repository.SaveChangesAsync(ct); throw new UnauthorizedException("Session is invalid."); }
        if (!old.IsRefreshValid(now) || !secrets.Verify(old.CsrfTokenHash, command.CsrfToken)) throw new UnauthorizedException("Session is invalid.");
        old.Revoke(now);
        var result = await ProvisioningAuthenticationTokenFactory.CreateAsync(repository, secrets,
            old.IdentityType, old.IdentityId, old.TenantId, old.FamilyId, now, options, ct);
        await repository.SaveChangesAsync(ct); return result;
    }
}

public sealed class ProvisioningChangeTemporaryPasswordCommandHandler(IAuthenticationRepository repository,
    IAuthenticationSecretProtector secrets, IPasswordHasher passwords, TimeProvider clock)
    : ICommandHandler<ChangeTemporaryPasswordCommand>
{
    public async Task HandleAsync(ChangeTemporaryPasswordCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var session = await repository.FindSessionByAccessHashAsync(secrets.Hash(command.AccessToken), ct)
            ?? throw new UnauthorizedException("Session is invalid.");
        if (!session.PasswordChangeRequired || !session.IsAccessValid(now)) throw new ForbiddenException("Password change is not allowed.");
        if (session.IdentityType == AuthenticationIdentityType.PlatformUser)
        {
            var user = await repository.GetPlatformUserAsync(session.IdentityId, ct) ?? throw new UnauthorizedException("Session is invalid.");
            if (!user.IsActive || !user.PasswordChangeRequired || !passwords.Verify(user.PasswordHash, command.CurrentPassword))
                throw new UnauthorizedException("Password change could not be completed.");
            user.ChangePassword(passwords.Hash(command.NewPassword), now);
        }
        else if (session.IdentityType == AuthenticationIdentityType.AdministrativeUser && session.TenantId is Guid tenantId)
        {
            var user = await repository.GetEligibleAdministrativeUserAsync(tenantId, session.IdentityId, ct)
                ?? throw new UnauthorizedException("Session is invalid.");
            if (!user.PasswordChangeRequired || !passwords.Verify(user.PasswordHash, command.CurrentPassword))
                throw new UnauthorizedException("Password change could not be completed.");
            user.ChangePassword(passwords.Hash(command.NewPassword), now);
        }
        else throw new ForbiddenException("Password change is not allowed.");
        await repository.RevokeIdentitySessionsAsync(session.IdentityType, session.IdentityId, now, ct);
        await repository.SaveChangesAsync(ct);
    }
}

internal static class ProvisioningAuthenticationTokenFactory
{
    public static async Task<AuthenticationTokens> CreateAsync(IAuthenticationRepository repository,
        IAuthenticationSecretProtector secrets, AuthenticationIdentityType type, Guid identityId,
        Guid? tenantId, Guid familyId, DateTimeOffset now, AuthenticationOptions options, CancellationToken ct)
    {
        bool restricted;
        if (type == AuthenticationIdentityType.PlatformUser)
        {
            var user = await repository.GetPlatformUserAsync(identityId, ct) ?? throw new UnauthorizedException("Session is invalid.");
            if (!user.IsActive) throw new UnauthorizedException("Session is invalid.");
            restricted = user.PasswordChangeRequired;
        }
        else if (type == AuthenticationIdentityType.AdministrativeUser && tenantId is Guid tenant)
        {
            var user = await repository.GetEligibleAdministrativeUserAsync(tenant, identityId, ct);
            if (user is null) { await repository.RevokeIdentitySessionsAsync(type, identityId, now, ct); await repository.SaveChangesAsync(ct); throw new UnauthorizedException("Session is invalid."); }
            restricted = user.PasswordChangeRequired;
        }
        else throw new UnauthorizedException("Session is invalid.");
        var access = secrets.GenerateToken(); var refresh = secrets.GenerateToken(); var csrf = secrets.GenerateToken();
        var session = AdministrativeSession.Create(familyId, type, identityId, tenantId, secrets.Hash(access),
            secrets.Hash(refresh), secrets.Hash(csrf), restricted, now, options.AccessLifetime, options.RefreshLifetime);
        await repository.AddSessionAsync(session, ct);
        return new(access, refresh, csrf, session.AccessExpiresAt, session.RefreshExpiresAt, restricted);
    }
}
