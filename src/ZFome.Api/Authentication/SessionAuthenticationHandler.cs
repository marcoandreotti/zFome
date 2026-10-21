using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Identity;
using ZFome.Domain.Identity;

namespace ZFome.Api.Authentication;

// Handler para autenticação baseada em sessão.
internal sealed class SessionAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IAuthenticationSessionResolver resolver) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ZFomeSession";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    { if (!Request.Cookies.TryGetValue(AuthenticationEndpoints.AccessCookie, out var token) || string.IsNullOrWhiteSpace(token)) return AuthenticateResult.NoResult(); var resolved = await resolver.ResolveAsync(token, Context.RequestAborted); if (resolved is null) return AuthenticateResult.Fail("Session is invalid."); var claims = new List<Claim> { new("sub", resolved.IdentityId.ToString()), new(ClaimTypes.NameIdentifier, resolved.IdentityId.ToString()), new("session_id", resolved.SessionId.ToString()), new("identity_type", resolved.Type.ToString()) }; if (resolved.TenantId is Guid tenantId) claims.Add(new("tenant_id", tenantId.ToString())); if (resolved.Type == AuthenticationIdentityType.PlatformUser) claims.Add(new("platform_user", "true")); foreach (var role in resolved.Roles) claims.Add(new(ClaimTypes.Role, role.ToString())); foreach (var establishment in resolved.EstablishmentIds) claims.Add(new("establishment_id", establishment.ToString())); if (resolved.PasswordChangeRequired) claims.Add(new("password_change_required", "true")); var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)); return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)); }
}