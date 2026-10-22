namespace ZFome.Contracts.Authentication;

public sealed record BeginAuthenticationRequest(string ContextCode, string Email, string Password);
public sealed record BeginAuthenticationResponse(Guid ChallengeId, DateTimeOffset ExpiresAt);
public sealed record CompleteAuthenticationRequest(Guid ChallengeId, string Code);
public sealed record AuthenticationResponse(DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt, bool PasswordChangeRequired);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record CreatePlatformUserRequest(string Email, string TemporaryPassword);
public sealed record SetPlatformUserActiveRequest(bool IsActive);
public sealed record AuthenticationEstablishmentResponse(Guid Id, string Name);
public sealed record AuthenticationContextResponse(bool PasswordChangeRequired, bool IsPlatformUser,
    IReadOnlyCollection<string> Capabilities, IReadOnlyCollection<AuthenticationEstablishmentResponse> Establishments);
