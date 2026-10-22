using FluentValidation;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Exceptions;
using ZFome.Domain.Identity;

namespace ZFome.Application.Identity.Authentication;

public sealed record BeginAuthenticationCommand(string ContextCode, string Email, string Password, string Origin) : ICommand<AuthenticationChallengeResult>;
public sealed record AuthenticationChallengeResult(Guid ChallengeId, DateTimeOffset ExpiresAt);
public sealed record CompleteAuthenticationCommand(Guid ChallengeId, string Code, string Origin) : ICommand<AuthenticationTokens>;
public sealed record RefreshAuthenticationCommand(string RefreshToken, string CsrfToken) : ICommand<AuthenticationTokens>;
public sealed record LogoutCommand(string AccessToken) : ICommand;
public sealed record ChangeTemporaryPasswordCommand(string AccessToken, string CurrentPassword, string NewPassword) : ICommand;
public sealed record CreatePlatformUserCommand(string AccessToken, string Email, string TemporaryPassword) : ICommand<Guid>;
public sealed record SetPlatformUserActiveCommand(string AccessToken, Guid PlatformUserId, bool IsActive) : ICommand;
public sealed record AuthenticationTokens(string AccessToken, string RefreshToken, string CsrfToken, DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt, bool PasswordChangeRequired);

public sealed class BeginAuthenticationCommandValidator : AbstractValidator<BeginAuthenticationCommand>
{
    public BeginAuthenticationCommandValidator() { RuleFor(x => x.ContextCode).NotEmpty().MaximumLength(50); RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150); RuleFor(x => x.Password).NotEmpty().MaximumLength(200); RuleFor(x => x.Origin).NotEmpty().MaximumLength(500); }
}
public sealed class CompleteAuthenticationCommandValidator : AbstractValidator<CompleteAuthenticationCommand>
{
    public CompleteAuthenticationCommandValidator() { RuleFor(x => x.ChallengeId).NotEmpty(); RuleFor(x => x.Code).Matches("^[0-9]{6}$"); RuleFor(x => x.Origin).NotEmpty().MaximumLength(500); }
}
public sealed class ChangeTemporaryPasswordCommandValidator : AbstractValidator<ChangeTemporaryPasswordCommand>
{
    public ChangeTemporaryPasswordCommandValidator() { RuleFor(x => x.AccessToken).NotEmpty(); RuleFor(x => x.CurrentPassword).NotEmpty(); RuleFor(x => x.NewPassword).MinimumLength(PasswordPolicy.MinimumLength).MaximumLength(PasswordPolicy.MaximumLength).NotEqual(x => x.CurrentPassword); }
}
public sealed class CreatePlatformUserCommandValidator : AbstractValidator<CreatePlatformUserCommand>
{
    public CreatePlatformUserCommandValidator() { RuleFor(x => x.AccessToken).NotEmpty(); RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150); RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(PasswordPolicy.MinimumLength).MaximumLength(PasswordPolicy.MaximumLength); }
}

public sealed class BeginAuthenticationCommandHandler(IAuthenticationRepository repository, IPasswordHasher passwords, IAuthenticationSecretProtector secrets, IAuthenticationCodeSender sender, TimeProvider clock, AuthenticationOptions options) : ICommandHandler<BeginAuthenticationCommand, AuthenticationChallengeResult>
{
    public async Task<AuthenticationChallengeResult> HandleAsync(BeginAuthenticationCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow(); var originHash = secrets.Hash(command.Origin); var normalizedEmail = new Email(command.Email).NormalizedValue;
        if (await repository.CountRecentChallengesAsync(originHash, now - options.RateLimitWindow, ct) >= options.MaximumChallengesPerWindow) throw new ConflictException("Authentication could not be completed.");
        AuthenticationIdentityType type; Guid identityId; Guid? tenantId; string email;
        if (string.Equals(command.ContextCode.Trim(), options.PlatformCode, StringComparison.OrdinalIgnoreCase))
        {
            var user = await repository.FindPlatformUserAsync(normalizedEmail, ct);
            if (user is null || !user.IsActive || !passwords.Verify(user.PasswordHash, command.Password)) throw new UnauthorizedException("Authentication could not be completed.");
            type = AuthenticationIdentityType.PlatformUser; identityId = user.Id; tenantId = null; email = user.Email.Value;
        }
        else
        {
            var match = await repository.FindAdministrativeUserAsync(command.ContextCode, normalizedEmail, ct);
            if (match is null || !match.Value.User.IsActive || !passwords.Verify(match.Value.User.PasswordHash, command.Password)) throw new UnauthorizedException("Authentication could not be completed.");
            type = AuthenticationIdentityType.AdministrativeUser; identityId = match.Value.User.Id; tenantId = match.Value.TenantId; email = match.Value.User.Email.Value;
        }
        var code = secrets.GenerateCode(); var challenge = AuthenticationChallenge.Create(type, identityId, tenantId, secrets.Hash(code), originHash, now, options.ChallengeLifetime);
        if (!await repository.ReplaceChallengeAsync(challenge, options.ResendInterval, ct)) throw new ConflictException("Authentication could not be completed.");
        await sender.SendAsync(email, code, challenge.ExpiresAt, ct);
        return new(challenge.Id, challenge.ExpiresAt);
    }
}

public sealed class CompleteAuthenticationCommandHandler(IAuthenticationRepository repository, IAuthenticationSecretProtector secrets, TimeProvider clock, AuthenticationOptions options) : ICommandHandler<CompleteAuthenticationCommand, AuthenticationTokens>
{
    public async Task<AuthenticationTokens> HandleAsync(CompleteAuthenticationCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow(); var challenge = await repository.GetChallengeAsync(command.ChallengeId, ct);
        if (challenge is null || !challenge.CanAttempt(now, options.MaximumCodeAttempts) || !secrets.Verify(challenge.CodeHash, command.Code) || !secrets.Verify(challenge.OriginHash, command.Origin))
        { if (challenge is not null && challenge.CanAttempt(now, options.MaximumCodeAttempts)) { challenge.Reject(now, options.MaximumCodeAttempts); await repository.SaveChangesAsync(ct); } throw new UnauthorizedException("Authentication could not be completed."); }
        challenge.Consume(now); var result = await AuthenticationTokenFactory.CreateAsync(repository, secrets, challenge.IdentityType, challenge.IdentityId, challenge.TenantId, Guid.NewGuid(), now, options, ct); await repository.SaveChangesAsync(ct); return result;
    }
}

public sealed class RefreshAuthenticationCommandHandler(IAuthenticationRepository repository, IAuthenticationSecretProtector secrets, TimeProvider clock, AuthenticationOptions options) : ICommandHandler<RefreshAuthenticationCommand, AuthenticationTokens>
{
    public async Task<AuthenticationTokens> HandleAsync(RefreshAuthenticationCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow(); var old = await repository.FindSessionByRefreshHashAsync(secrets.Hash(command.RefreshToken), ct) ?? throw new UnauthorizedException("Session is invalid.");
        if (old.RevokedAt is not null) { await repository.RevokeFamilyAsync(old.FamilyId, now, ct); await repository.SaveChangesAsync(ct); throw new UnauthorizedException("Session is invalid."); }
        if (!old.IsRefreshValid(now) || !secrets.Verify(old.CsrfTokenHash, command.CsrfToken)) throw new UnauthorizedException("Session is invalid.");
        old.Revoke(now); var result = await AuthenticationTokenFactory.CreateAsync(repository, secrets, old.IdentityType, old.IdentityId, old.TenantId, old.FamilyId, now, options, ct); await repository.SaveChangesAsync(ct); return result;
    }
}

public sealed class LogoutCommandHandler(IAuthenticationRepository repository, IAuthenticationSecretProtector secrets, TimeProvider clock) : ICommandHandler<LogoutCommand>
{ public async Task HandleAsync(LogoutCommand command, CancellationToken ct) { var session = await repository.FindSessionByAccessHashAsync(secrets.Hash(command.AccessToken), ct); if (session is null) return; await repository.RevokeFamilyAsync(session.FamilyId, clock.GetUtcNow(), ct); await repository.SaveChangesAsync(ct); } }

public sealed class ChangeTemporaryPasswordCommandHandler(IAuthenticationRepository repository, IAuthenticationSecretProtector secrets, IPasswordHasher passwords, TimeProvider clock) : ICommandHandler<ChangeTemporaryPasswordCommand>
{
    public async Task HandleAsync(ChangeTemporaryPasswordCommand command, CancellationToken ct) { var now = clock.GetUtcNow(); var session = await repository.FindSessionByAccessHashAsync(secrets.Hash(command.AccessToken), ct) ?? throw new UnauthorizedException("Session is invalid."); if (session.IdentityType != AuthenticationIdentityType.PlatformUser || !session.PasswordChangeRequired || !session.IsAccessValid(now)) throw new ForbiddenException("Password change is not allowed."); var user = await repository.GetPlatformUserAsync(session.IdentityId, ct) ?? throw new UnauthorizedException("Session is invalid."); if (!user.IsActive || !user.PasswordChangeRequired || !passwords.Verify(user.PasswordHash, command.CurrentPassword)) throw new UnauthorizedException("Password change could not be completed."); user.ChangePassword(passwords.Hash(command.NewPassword), now); await repository.RevokeIdentitySessionsAsync(session.IdentityType, session.IdentityId, now, ct); await repository.SaveChangesAsync(ct); }
}

public sealed class CreatePlatformUserCommandHandler(IAuthenticationSessionResolver resolver, IAuthenticationRepository repository, IPasswordHasher passwords, TimeProvider clock) : ICommandHandler<CreatePlatformUserCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreatePlatformUserCommand command, CancellationToken ct) { var actor = await resolver.ResolveAsync(command.AccessToken, ct); if (actor is null || actor.Type != AuthenticationIdentityType.PlatformUser || actor.PasswordChangeRequired) throw new ForbiddenException("A fully authenticated platform user is required."); var email = new Email(command.Email); if (await repository.FindPlatformUserAsync(email.NormalizedValue, ct) is not null) throw new ConflictException("Platform user email is already in use."); var user = PlatformUser.Create(email, passwords.Hash(command.TemporaryPassword), clock.GetUtcNow()); await repository.AddPlatformUserAsync(user, ct); await repository.SaveChangesAsync(ct); return user.Id; }
}
public sealed class SetPlatformUserActiveCommandHandler(IAuthenticationSessionResolver resolver, IAuthenticationRepository repository, TimeProvider clock) : ICommandHandler<SetPlatformUserActiveCommand>
{
    public async Task HandleAsync(SetPlatformUserActiveCommand command, CancellationToken ct) { var actor = await resolver.ResolveAsync(command.AccessToken, ct); if (actor is null || actor.Type != AuthenticationIdentityType.PlatformUser || actor.PasswordChangeRequired) throw new ForbiddenException("A fully authenticated platform user is required."); var target = await repository.GetPlatformUserAsync(command.PlatformUserId, ct) ?? throw new NotFoundException("Platform user was not found."); if (!command.IsActive) { if (target.Id == actor.IdentityId) throw new ConflictException("A platform user cannot deactivate the current identity."); if (target.IsActive && await repository.CountActivePlatformUsersAsync(ct) <= 1) throw new ConflictException("At least one active platform user is required."); target.Deactivate(clock.GetUtcNow()); } else target.Activate(clock.GetUtcNow()); await repository.SaveChangesAsync(ct); }
}

internal static class AuthenticationTokenFactory
{
    public static async Task<AuthenticationTokens> CreateAsync(IAuthenticationRepository repository, IAuthenticationSecretProtector secrets, AuthenticationIdentityType type, Guid identityId, Guid? tenantId, Guid familyId, DateTimeOffset now, AuthenticationOptions options, CancellationToken ct)
    {
        var restricted = false;
        if (type == AuthenticationIdentityType.PlatformUser)
        {
            var user = await repository.GetPlatformUserAsync(identityId, ct) ?? throw new UnauthorizedException("Session is invalid.");
            if (!user.IsActive) throw new UnauthorizedException("Session is invalid.");
            restricted = user.PasswordChangeRequired;
        }
        else if (type != AuthenticationIdentityType.AdministrativeUser || tenantId is not Guid tenant ||
                 await repository.GetEligibleAdministrativeUserAsync(tenant, identityId, ct) is null)
        {
            await repository.RevokeIdentitySessionsAsync(type, identityId, now, ct);
            await repository.SaveChangesAsync(ct);
            throw new UnauthorizedException("Session is invalid.");
        }
        var access = secrets.GenerateToken(); var refresh = secrets.GenerateToken(); var csrf = secrets.GenerateToken();
        var session = AdministrativeSession.Create(familyId, type, identityId, tenantId, secrets.Hash(access), secrets.Hash(refresh), secrets.Hash(csrf), restricted, now, options.AccessLifetime, options.RefreshLifetime);
        await repository.AddSessionAsync(session, ct);
        return new(access, refresh, csrf, session.AccessExpiresAt, session.RefreshExpiresAt, restricted);
    }
}

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    public string PlatformCode { get; set; } = "PLATFORM";
    public TimeSpan ChallengeLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan ResendInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan AccessLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshLifetime { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(15);
    public int MaximumChallengesPerWindow { get; set; } = 5;
    public int MaximumCodeAttempts { get; set; } = 5;
}
