using Microsoft.Extensions.Primitives;

namespace ZFome.Api.Middleware;

/// <summary>
/// Middleware que adiciona um ID de correlação aos cabeçalhos de requisição e resposta, facilitando o rastreamento de requisições entre diferentes serviços.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    // O método InvokeAsync é chamado para cada requisição HTTP.
    // Ele resolve o ID de correlação a partir dos cabeçalhos da requisição, define o TraceIdentifier do contexto e adiciona o ID de correlação aos cabeçalhos da resposta.
    // Além disso, ele cria um escopo de log com o ID de correlação para facilitar o rastreamento nos logs.
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName]);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    // Método auxiliar para resolver o ID de correlação a partir dos cabeçalhos da requisição. Se não houver um ID válido, gera um novo GUID.
    private static string ResolveCorrelationId(StringValues values) =>
        values.Count == 1 && !string.IsNullOrWhiteSpace(values[0]) ? values[0]! : Guid.NewGuid().ToString("N");
}