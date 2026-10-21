using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ZFome.Api.Tenancy;

// Esse handler de autenticação é usado para permitir que o ASP.NET Core reconheça um usuário já autenticado (por exemplo, via Windows Authentication)
// como um usuário autenticado dentro do contexto da aplicação. Ele verifica se o usuário atual (Context.User) está autenticado e, se estiver,
// cria um ticket de autenticação para ele. Caso contrário, retorna um resultado indicando que não há autenticação.
internal sealed class ExistingPrincipalAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ExistingPrincipal";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.User.Identity?.IsAuthenticated == true)
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Context.User, SchemeName)));
        return Task.FromResult(AuthenticateResult.NoResult());
    }
}