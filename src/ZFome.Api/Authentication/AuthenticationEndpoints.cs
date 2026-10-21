using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Identity.Authentication;
using ZFome.Contracts.Authentication;

namespace ZFome.Api.Authentication;

internal static class AuthenticationEndpoints
{
    internal const string AccessCookie = "zf_access"; internal const string RefreshCookie = "zf_refresh"; internal const string CsrfCookie = "zf_csrf";
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    { var group = endpoints.MapGroup("/api/auth").AllowAnonymous().WithTags("Authentication"); group.MapGet("/context", ContextAsync); group.MapPost("/begin", BeginAsync).RequireRateLimiting("authentication-attempts"); group.MapPost("/complete", CompleteAsync).RequireRateLimiting("authentication-attempts"); group.MapPost("/refresh", RefreshAsync); group.MapPost("/logout", LogoutAsync); group.MapPost("/change-password", ChangePasswordAsync); group.MapPost("/platform-users", CreatePlatformUserAsync); group.MapPatch("/platform-users/{id:guid}/active", SetPlatformUserActiveAsync); return endpoints; }
    private static async Task<IResult> ContextAsync(HttpContext context, IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.DispatchAsync<GetAuthenticationContextQuery, AuthenticationContext>(new(Cookie(context, AccessCookie)), ct);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new AuthenticationContextResponse(result.PasswordChangeRequired, result.IsPlatformUser,
            result.Capabilities, result.Establishments.Select(item => new AuthenticationEstablishmentResponse(item.Id, item.Name)).ToArray()));
    }
    private static string Origin(HttpContext c) => $"{c.Connection.RemoteIpAddress}|{c.Request.Headers.UserAgent}";
    private static async Task<IResult> BeginAsync(BeginAuthenticationRequest r, HttpContext c, ICommandDispatcher d, CancellationToken ct) { var x = await d.DispatchAsync<BeginAuthenticationCommand, AuthenticationChallengeResult>(new(r.ContextCode, r.Email, r.Password, Origin(c)), ct); return Results.Accepted(value: new BeginAuthenticationResponse(x.ChallengeId, x.ExpiresAt)); }
    private static async Task<IResult> CompleteAsync(CompleteAuthenticationRequest r, HttpContext c, ICommandDispatcher d, CancellationToken ct) { var x = await d.DispatchAsync<CompleteAuthenticationCommand, AuthenticationTokens>(new(r.ChallengeId, r.Code, Origin(c)), ct); WriteCookies(c, x); return Results.Ok(new AuthenticationResponse(x.AccessExpiresAt, x.RefreshExpiresAt, x.PasswordChangeRequired)); }
    private static async Task<IResult> RefreshAsync(HttpContext c, ICommandDispatcher d, CancellationToken ct) { var refresh = Cookie(c, RefreshCookie); var csrfHeader = c.Request.Headers["X-CSRF-Token"].ToString(); var csrfCookie = Cookie(c, CsrfCookie); if (!string.Equals(csrfHeader, csrfCookie, StringComparison.Ordinal)) return Results.Unauthorized(); var x = await d.DispatchAsync<RefreshAuthenticationCommand, AuthenticationTokens>(new(refresh, csrfHeader), ct); WriteCookies(c, x); return Results.Ok(new AuthenticationResponse(x.AccessExpiresAt, x.RefreshExpiresAt, x.PasswordChangeRequired)); }
    private static async Task<IResult> LogoutAsync(HttpContext c, ICommandDispatcher d, CancellationToken ct) { var token = Cookie(c, AccessCookie); await d.DispatchAsync(new LogoutCommand(token), ct); ClearCookies(c); return Results.NoContent(); }
    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest r, HttpContext c, ICommandDispatcher d, CancellationToken ct) { await d.DispatchAsync(new ChangeTemporaryPasswordCommand(Cookie(c, AccessCookie), r.CurrentPassword, r.NewPassword), ct); ClearCookies(c); return Results.NoContent(); }
    private static async Task<IResult> CreatePlatformUserAsync(CreatePlatformUserRequest r, HttpContext c, ICommandDispatcher d, CancellationToken ct) => Results.Ok(new { id = await d.DispatchAsync<CreatePlatformUserCommand, Guid>(new(Cookie(c, AccessCookie), r.Email, r.TemporaryPassword), ct) });
    private static async Task<IResult> SetPlatformUserActiveAsync(Guid id, SetPlatformUserActiveRequest r, HttpContext c, ICommandDispatcher d, CancellationToken ct) { await d.DispatchAsync(new SetPlatformUserActiveCommand(Cookie(c, AccessCookie), id, r.IsActive), ct); return Results.NoContent(); }
    private static string Cookie(HttpContext c, string name) => c.Request.Cookies.TryGetValue(name, out var value) ? value : string.Empty;
    private static void WriteCookies(HttpContext c, AuthenticationTokens x) { var secure = c.Request.IsHttps; c.Response.Cookies.Append(AccessCookie, x.AccessToken, new() { HttpOnly = true, Secure = secure, SameSite = SameSiteMode.Strict, Expires = x.AccessExpiresAt }); c.Response.Cookies.Append(RefreshCookie, x.RefreshToken, new() { HttpOnly = true, Secure = secure, SameSite = SameSiteMode.Strict, Expires = x.RefreshExpiresAt }); c.Response.Cookies.Append(CsrfCookie, x.CsrfToken, new() { HttpOnly = false, Secure = secure, SameSite = SameSiteMode.Strict, Expires = x.RefreshExpiresAt }); }
    private static void ClearCookies(HttpContext c) { c.Response.Cookies.Delete(AccessCookie); c.Response.Cookies.Delete(RefreshCookie); c.Response.Cookies.Delete(CsrfCookie); }
}
