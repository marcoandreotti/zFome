namespace ZFome.Contracts.Administration;

public sealed record EstablishmentUpdateRequest(string TradeName, string Slug, string TimeZoneId = "America/Sao_Paulo");
public sealed record EstablishmentThemeRequest(string? PrimaryColor = null, string? SecondaryColor = null, string? BackgroundColor = null, string? TextColor = null, string? FontFamily = null, string? LogoUrl = null, string? FaviconUrl = null);
public sealed record BusinessHoursRequest(int DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);
public sealed record ReplaceBusinessHoursRequest(IReadOnlyList<BusinessHoursRequest> Hours);
public sealed record ScheduleExceptionRequest(DateOnly Date, string? ServiceType, bool IsOpen, TimeOnly? OpensAt, TimeOnly? ClosesAt, string? Reason);
public sealed record ReplaceScheduleExceptionsRequest(IReadOnlyList<ScheduleExceptionRequest> Exceptions);
public sealed record PauseServiceRequest(string ServiceType, DateTimeOffset? EndsAt, string? Reason);
public sealed record OfferUnavailabilityRequest(DateTimeOffset? EndsAt, string? Reason);
public sealed record CreateTableRequest(Guid IntentId, string Code, string? Description);
public sealed record UpdateTableRequest(string Code, string? Description, bool IsActive);
