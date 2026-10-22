using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ZFome.Application.Exceptions;
using ZFome.Domain.Exceptions;
using ApplicationValidationException = ZFome.Application.Exceptions.ValidationException;

namespace ZFome.Api.Middleware;

/// <summary>
/// Middleware para capturar exceções globais e retornar respostas padronizadas de erro.
/// </summary>
internal sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemDetailsAsync(context, exception);
        }
    }

    // Método para escrever detalhes do problema na resposta HTTP com base na exceção capturada.
    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        var (status, title) = exception switch
        {
            ApplicationValidationException or FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, "Falha na validação"),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflito"),
            AvailabilityConflictException => (StatusCodes.Status409Conflict, "Conflito de disponibilidade"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Não autenticado"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio não atendida"),
            _ => (StatusCodes.Status500InternalServerError, "Erro inesperado")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for correlation ID {CorrelationId}", context.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(exception, "Request failed for correlation ID {CorrelationId}", context.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= StatusCodes.Status500InternalServerError ? null : exception switch
            {
                ApplicationValidationException or FluentValidation.ValidationException => "Revise os campos destacados e tente novamente.",
                NotFoundException => "O item solicitado não foi encontrado.",
                AvailabilityConflictException availability => availability.Reason switch
                {
                    ZFome.Domain.Operations.AvailabilityReason.OutsideBusinessHours => "Estamos fora do horário de atendimento.",
                    ZFome.Domain.Operations.AvailabilityReason.CalendarException => "A unidade está fechada excepcionalmente.",
                    ZFome.Domain.Operations.AvailabilityReason.ServicePaused => "Esta modalidade está temporariamente pausada.",
                    ZFome.Domain.Operations.AvailabilityReason.EstablishmentInactive => "A unidade não está recebendo pedidos.",
                    _ => "Esta modalidade não está disponível agora."
                },
                ConflictException => "A operação não pode ser concluída no estado atual.",
                UnauthorizedException => "Autentique-se para continuar.",
                ForbiddenException => "Você não tem permissão para realizar esta operação.",
                DomainException => "Os dados informados não atendem às regras do sistema.",
                _ => "Não foi possível concluir a solicitação."
            },
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (exception is ApplicationValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors;
        }
        else if (exception is FluentValidation.ValidationException fluentValidationException)
        {
            problem.Extensions["errors"] = fluentValidationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
        }
        if (exception is AvailabilityConflictException availabilityException)
        {
            problem.Extensions["reason"] = availabilityException.Reason.ToString();
            problem.Extensions["nextOpening"] = availabilityException.NextOpening;
            problem.Extensions["affectedOfferIds"] = availabilityException.AffectedOfferIds;
        }
        if (exception is DeliveryQuoteConflictException deliveryQuoteException)
        {
            problem.Extensions["currentDeliveryFee"] = deliveryQuoteException.CurrentFee;
            problem.Extensions["currentTotal"] = deliveryQuoteException.CurrentTotal;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
    }
}
