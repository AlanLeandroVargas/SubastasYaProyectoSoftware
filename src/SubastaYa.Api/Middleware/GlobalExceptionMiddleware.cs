using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SubastaYa.Domain.Exceptions;

namespace SubastaYa.Api.Middleware;

/// <summary>
/// Traduce las excepciones del dominio al código HTTP que corresponde a cada situación.
/// Concentrar la traducción acá evita bloques try/catch repetidos en los controladores y
/// garantiza que un error de negocio nunca se degrade en un 500 genérico.
/// Los títulos y detalles van en español porque se muestran tal cual en la interfaz.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException exception)
        {
            await RespondAsync(context, exception, ToProblemDetails(exception));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error no controlado al procesar {Path}.", context.Request.Path);

            await RespondAsync(context, exception, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor",
                Detail = "Ocurrió un error inesperado al procesar la solicitud."
            });
        }
    }

    private static ProblemDetails ToProblemDetails(DomainException exception) => exception switch
    {
        ValidationException => Build(StatusCodes.Status400BadRequest, "Solicitud inválida", exception.Message),
        InvalidCredentialsException => Build(StatusCodes.Status401Unauthorized, "Credenciales inválidas", exception.Message),
        AuthorizationException => Build(StatusCodes.Status403Forbidden, "Operación no permitida", exception.Message),
        ResourceNotFoundException => Build(StatusCodes.Status404NotFound, "Recurso inexistente", exception.Message),
        StateConflictException => Build(StatusCodes.Status409Conflict, "Conflicto con el estado actual", exception.Message),
        InsufficientFundsException => Build(StatusCodes.Status422UnprocessableEntity, "Saldo insuficiente", exception.Message),
        _ => Build(StatusCodes.Status400BadRequest, "Solicitud inválida", exception.Message)
    };

    private static ProblemDetails Build(int statusCode, string title, string detail) => new()
    {
        Status = statusCode,
        Title = title,
        Detail = detail
    };

    private async Task RespondAsync(HttpContext context, Exception exception, ProblemDetails problem)
    {
        if (context.Response.HasStarted)
        {
            // La respuesta ya empezó a escribirse: no es posible reemplazar el código de estado.
            _logger.LogWarning(exception, "No se pudo devolver el error: la respuesta ya había comenzado.");
            return;
        }

        problem.Instance = context.Request.Path;
        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
