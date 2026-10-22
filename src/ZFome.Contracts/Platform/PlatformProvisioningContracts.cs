namespace ZFome.Contracts.Platform;

public sealed record ProvisionTenantRequest(Guid IntentKey, string TenantName, string TenantPublicCode,
    string EstablishmentName, string EstablishmentSlug, string TimeZoneId,
    string OwnerName, string OwnerEmail, string TemporaryPassword);
public sealed record AddPlatformEstablishmentRequest(Guid IntentKey, string Name, string Slug,
    string TimeZoneId, Guid OwnerId);
public sealed record PlatformProvisioningResponse(Guid TenantId, Guid EstablishmentId, Guid OwnerId,
    string TenantPublicCode, string NextStep);
public sealed record PlatformEstablishmentResponse(Guid Id, string Name, string Slug,
    bool IsActive, bool OnboardingCompleted);
public sealed record PlatformTenantResponse(Guid Id, string Name, string PublicCode, bool IsActive,
    IReadOnlyList<PlatformEstablishmentResponse> Establishments);
public sealed record PlatformTenantPageResponse(IReadOnlyList<PlatformTenantResponse> Items,
    long TotalCount, int Page, int PageSize);
