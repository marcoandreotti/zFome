using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;

namespace ZFome.Application.Platform;

public sealed record PlatformProvisioningResult(Guid TenantId, Guid EstablishmentId, Guid OwnerId, string TenantPublicCode);
public sealed record ProvisionTenantCommand(string AccessToken, Guid IntentKey, string TenantName, string TenantPublicCode,
    string EstablishmentName, string EstablishmentSlug, string TimeZoneId, string OwnerName,
    string OwnerEmail, string TemporaryPassword) : ICommand<PlatformProvisioningResult>;
public sealed record AddPlatformEstablishmentCommand(string AccessToken, Guid TenantId, Guid IntentKey,
    string Name, string Slug, string TimeZoneId, Guid OwnerId) : ICommand<PlatformProvisioningResult>;
public sealed record SearchPlatformTenantsQuery(string AccessToken, string? Search, bool? IsActive,
    int Page, int PageSize) : IQuery<PlatformTenantSearchResult>;
public sealed record GetPlatformTenantQuery(string AccessToken, Guid TenantId) : IQuery<PlatformTenantReadModel>;
public sealed record GetPlatformProvisioningQuery(string AccessToken, Guid IntentKey) : IQuery<PlatformProvisioningResult>;

public sealed record PlatformEstablishmentReadModel(Guid Id, string Name, string Slug, bool IsActive, bool OnboardingCompleted);
public sealed record PlatformTenantReadModel(Guid Id, string Name, string PublicCode, bool IsActive,
    IReadOnlyList<PlatformEstablishmentReadModel> Establishments);
public sealed record PlatformTenantSearchResult(IReadOnlyList<PlatformTenantReadModel> Items, long TotalCount, int Page, int PageSize);

public sealed class ProvisionTenantCommandValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantCommandValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty(); RuleFor(x => x.IntentKey).NotEmpty();
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TenantPublicCode).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.EstablishmentName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.EstablishmentSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.TemporaryPassword).MinimumLength(PasswordPolicy.MinimumLength).MaximumLength(PasswordPolicy.MaximumLength);
    }
}

public sealed class AddPlatformEstablishmentCommandValidator : AbstractValidator<AddPlatformEstablishmentCommand>
{
    public AddPlatformEstablishmentCommandValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty(); RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.IntentKey).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100); RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerId).NotEmpty();
    }
}

public sealed class SearchPlatformTenantsQueryValidator : AbstractValidator<SearchPlatformTenantsQuery>
{
    public SearchPlatformTenantsQueryValidator()
    { RuleFor(x => x.AccessToken).NotEmpty(); RuleFor(x => x.Search).MaximumLength(150); RuleFor(x => x.Page).GreaterThan(0); RuleFor(x => x.PageSize).InclusiveBetween(1, 100); }
}

public sealed class GetPlatformTenantQueryValidator : AbstractValidator<GetPlatformTenantQuery>
{
    public GetPlatformTenantQueryValidator() { RuleFor(x => x.AccessToken).NotEmpty(); RuleFor(x => x.TenantId).NotEmpty(); }
}

public sealed class GetPlatformProvisioningQueryValidator : AbstractValidator<GetPlatformProvisioningQuery>
{
    public GetPlatformProvisioningQueryValidator() { RuleFor(x => x.AccessToken).NotEmpty(); RuleFor(x => x.IntentKey).NotEmpty(); }
}

internal static class PlatformAuthorization
{
    public static async Task<AuthenticatedIdentity> RequireAsync(IAuthenticationSessionResolver resolver,
        string accessToken, CancellationToken ct)
    {
        var actor = await resolver.ResolveAsync(accessToken, ct);
        if (actor is null || actor.Type != AuthenticationIdentityType.PlatformUser || actor.PasswordChangeRequired)
            throw new ForbiddenException("A fully authenticated platform user is required.");
        return actor;
    }
}

internal static class ProvisioningHash
{
    public static string Create(params object?[] values)
    {
        var canonical = string.Join('\u001f', values.Select(value => value switch
        {
            string text => text.Trim().ToUpperInvariant(),
            null => string.Empty,
            _ => value.ToString()
        }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed class ProvisionTenantCommandHandler(IAuthenticationSessionResolver resolver,
    IPlatformProvisioningRepository repository, IPlatformProvisioningTransaction transaction,
    IPasswordHasher passwords, TimeProvider clock) : ICommandHandler<ProvisionTenantCommand, PlatformProvisioningResult>
{
    public async Task<PlatformProvisioningResult> HandleAsync(ProvisionTenantCommand command, CancellationToken ct)
    {
        var actor = await PlatformAuthorization.RequireAsync(resolver, command.AccessToken, ct);
        var hash = ProvisioningHash.Create(command.TenantName, command.TenantPublicCode, command.EstablishmentName,
            command.EstablishmentSlug, command.TimeZoneId, command.OwnerName, command.OwnerEmail, command.TemporaryPassword);
        return await transaction.ExecuteAsync(async token =>
        {
            var existing = await repository.GetIntentAsync(actor.IdentityId, command.IntentKey, token);
            if (existing is not null) { var result = Existing(existing, hash, PlatformProvisioningKind.Tenant); var existingTenant = await repository.GetTenantAsync(result.TenantId, token); return result with { TenantPublicCode = existingTenant?.PublicCode ?? string.Empty }; }
            var publicCode = Tenant.NormalizePublicCode(command.TenantPublicCode);
            var slug = new Slug(command.EstablishmentSlug);
            if (await repository.TenantPublicCodeExistsAsync(publicCode, token)) throw new ConflictException("Tenant public code is already in use.");
            if (await repository.EstablishmentSlugExistsAsync(slug.Value, token)) throw new ConflictException("The establishment slug is already in use.");
            var now = clock.GetUtcNow();
            var tenant = Tenant.Create(command.TenantName, publicCode, now);
            var establishment = Establishment.Create(tenant.Id, command.EstablishmentName, slug, now);
            establishment.ChangeTimeZone(command.TimeZoneId, now);
            var owner = AdministrativeUser.CreateProvisioned(tenant.Id, command.OwnerName,
                new Email(command.OwnerEmail), passwords.Hash(command.TemporaryPassword), now);
            owner.GrantEstablishmentAccess(establishment.Id, tenant.Id, now);
            var intent = PlatformProvisioningIntent.Create(actor.IdentityId, command.IntentKey, hash,
                PlatformProvisioningKind.Tenant, now);
            intent.Complete(tenant.Id, establishment.Id, owner.Id, now);
            await repository.AddTenantProvisioningAsync(tenant, establishment, owner, intent, token);
            return new(tenant.Id, establishment.Id, owner.Id, tenant.PublicCode);
        }, ct);
    }

    internal static PlatformProvisioningResult Existing(PlatformProvisioningIntent intent, string hash,
        PlatformProvisioningKind kind)
    {
        if (intent.RequestHash != hash || intent.Kind != kind) throw new ConflictException("Provisioning key was reused with different data.");
        if (intent is not { TenantId: Guid tenantId, EstablishmentId: Guid establishmentId, OwnerId: Guid ownerId, CompletedAt: not null })
            throw new ConflictException("Provisioning is incomplete.");
        return new(tenantId, establishmentId, ownerId, string.Empty);
    }
}

public sealed class AddPlatformEstablishmentCommandHandler(IAuthenticationSessionResolver resolver,
    IPlatformProvisioningRepository repository, IPlatformProvisioningTransaction transaction,
    TimeProvider clock) : ICommandHandler<AddPlatformEstablishmentCommand, PlatformProvisioningResult>
{
    public async Task<PlatformProvisioningResult> HandleAsync(AddPlatformEstablishmentCommand command, CancellationToken ct)
    {
        var actor = await PlatformAuthorization.RequireAsync(resolver, command.AccessToken, ct);
        var hash = ProvisioningHash.Create(command.TenantId, command.Name, command.Slug, command.TimeZoneId, command.OwnerId);
        return await transaction.ExecuteAsync(async token =>
        {
            var existing = await repository.GetIntentAsync(actor.IdentityId, command.IntentKey, token);
            if (existing is not null) { var result = ProvisionTenantCommandHandler.Existing(existing, hash, PlatformProvisioningKind.Establishment); var existingTenant = await repository.GetTenantAsync(result.TenantId, token); return result with { TenantPublicCode = existingTenant?.PublicCode ?? string.Empty }; }
            var tenant = await repository.GetTenantAsync(command.TenantId, token);
            if (tenant is null || !tenant.IsActive) throw new NotFoundException("Tenant was not found.");
            var owner = await repository.GetOwnerAsync(tenant.Id, command.OwnerId, token)
                ?? throw new NotFoundException("Eligible Owner was not found.");
            var slug = new Slug(command.Slug);
            if (await repository.EstablishmentSlugExistsAsync(slug.Value, token)) throw new ConflictException("The establishment slug is already in use.");
            var now = clock.GetUtcNow();
            var establishment = Establishment.Create(tenant.Id, command.Name, slug, now);
            establishment.ChangeTimeZone(command.TimeZoneId, now);
            owner.GrantEstablishmentAccess(establishment.Id, tenant.Id, now);
            var intent = PlatformProvisioningIntent.Create(actor.IdentityId, command.IntentKey, hash,
                PlatformProvisioningKind.Establishment, now);
            intent.Complete(tenant.Id, establishment.Id, owner.Id, now);
            await repository.AddEstablishmentProvisioningAsync(establishment, intent, token);
            return new(tenant.Id, establishment.Id, owner.Id, tenant.PublicCode);
        }, ct);
    }
}

public sealed class SearchPlatformTenantsQueryHandler(IAuthenticationSessionResolver resolver,
    IPlatformProvisioningReadGateway gateway) : IQueryHandler<SearchPlatformTenantsQuery, PlatformTenantSearchResult>
{
    public async Task<PlatformTenantSearchResult> HandleAsync(SearchPlatformTenantsQuery query, CancellationToken ct)
    { await PlatformAuthorization.RequireAsync(resolver, query.AccessToken, ct); return await gateway.SearchTenantsAsync(query.Search, query.IsActive, query.Page, query.PageSize, ct); }
}

public sealed class GetPlatformTenantQueryHandler(IAuthenticationSessionResolver resolver,
    IPlatformProvisioningReadGateway gateway) : IQueryHandler<GetPlatformTenantQuery, PlatformTenantReadModel>
{
    public async Task<PlatformTenantReadModel> HandleAsync(GetPlatformTenantQuery query, CancellationToken ct)
    { await PlatformAuthorization.RequireAsync(resolver, query.AccessToken, ct); return await gateway.GetTenantAsync(query.TenantId, ct) ?? throw new NotFoundException("Tenant was not found."); }
}

public sealed class GetPlatformProvisioningQueryHandler(IAuthenticationSessionResolver resolver,
    IPlatformProvisioningRepository repository) : IQueryHandler<GetPlatformProvisioningQuery, PlatformProvisioningResult>
{
    public async Task<PlatformProvisioningResult> HandleAsync(GetPlatformProvisioningQuery query, CancellationToken ct)
    {
        var actor = await PlatformAuthorization.RequireAsync(resolver, query.AccessToken, ct);
        var intent = await repository.GetIntentAsync(actor.IdentityId, query.IntentKey, ct) ?? throw new NotFoundException("Provisioning intent was not found.");
        if (intent is not { TenantId: Guid tenantId, EstablishmentId: Guid establishmentId, OwnerId: Guid ownerId, CompletedAt: not null })
            throw new NotFoundException("Provisioning intent was not found.");
        var tenant = await repository.GetTenantAsync(tenantId, ct);
        return new(tenantId, establishmentId, ownerId, tenant?.PublicCode ?? string.Empty);
    }
}
