namespace ZFome.Contracts.Administration;

public sealed record AdministrativeUserCreateRequest(string Name, string Email, string Password, short InitialRole);
public sealed record AdministrativeUserUpdateRequest(string Name);
public sealed record AdministrativeUserActiveRequest(bool IsActive);
public sealed record AdministrativeUserGrantRequest(bool Granted);
public sealed record AdministrativeUserResponse(Guid Id, string Name, string Email, bool IsActive, short[] Roles, Guid[] EstablishmentIds, bool IsCurrentUser);
public sealed record AdministrativeUserPageResponse(IReadOnlyList<AdministrativeUserResponse> Items, long TotalCount, int Page, int PageSize);
