using ZFome.Api.Authentication;

namespace ZFome.Api.Middleware;

// Esse middleware é responsável por verificar se o usuário autenticado precisa alterar a senha e se a solicitação é mutável (POST, PUT, PATCH, DELETE).
// Se o usuário precisar alterar a senha e estiver tentando acessar um endpoint que não seja permitido, ele retornará um status 403 Forbidden.
// Além disso, ele verifica se a solicitação mutável possui um token CSRF válido.
internal sealed class AuthenticationSecurityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.HasClaim("password_change_required", "true") &&
           !(HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/api/auth/context") &&
           !context.Request.Path.StartsWithSegments("/api/auth/change-password") && !context.Request.Path.StartsWithSegments("/api/auth/logout"))
        { context.Response.StatusCode = StatusCodes.Status403Forbidden; await context.Response.WriteAsJsonAsync(new { title = "Password change required", status = 403, traceId = context.TraceIdentifier }, context.RequestAborted); return; }
        var isMutable = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method);
        if (context.User.Identity?.IsAuthenticated == true && context.User.HasClaim(claim => claim.Type == "session_id") && isMutable)
        { if (!context.Request.Path.StartsWithSegments("/api/auth") && (!context.Request.Cookies.TryGetValue(AuthenticationEndpoints.CsrfCookie, out var cookie) || !string.Equals(cookie, context.Request.Headers["X-CSRF-Token"], StringComparison.Ordinal))) { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; } }
        await next(context);
    }
}