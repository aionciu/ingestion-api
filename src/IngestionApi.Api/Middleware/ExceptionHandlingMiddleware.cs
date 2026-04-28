using System.Text.Json;
using FluentValidation;

namespace IngestionApi.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (severity, statusCode) = CategorizeException(exception);

        _logger.Log(severity, exception, "Unhandled exception: {ExceptionType} → {StatusCode}", exception.GetType().Name, statusCode);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        object response;
        if (statusCode is StatusCodes.Status422UnprocessableEntity or StatusCodes.Status400BadRequest)
        {
            // Safe to expose message for known validation/argument errors
            response = new { error = exception.Message };
        }
        else if (statusCode == StatusCodes.Status503ServiceUnavailable)
        {
            response = new { detail = "Service temporarily unavailable" };
        }
        else
        {
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? string.Empty;
            response = new { detail = "Internal error", traceId = correlationId };
        }

        await context.Response.WriteAsJsonAsync(response);
    }

    private static (LogLevel, int) CategorizeException(Exception exception) => exception switch
    {
        // Critical (Fatal) - system in invalid state
        InvalidOperationException => (LogLevel.Critical, StatusCodes.Status500InternalServerError),
        NullReferenceException => (LogLevel.Critical, StatusCodes.Status500InternalServerError),

        // High (Error) - validation/business rule failures
        ValidationException => (LogLevel.Error, StatusCodes.Status422UnprocessableEntity),
        ArgumentException => (LogLevel.Error, StatusCodes.Status400BadRequest),

        // Medium (Warning) - temporary operational issues
        TimeoutException => (LogLevel.Warning, StatusCodes.Status503ServiceUnavailable),
        IOException => (LogLevel.Warning, StatusCodes.Status503ServiceUnavailable),

        // Default - treat as error
        _ => (LogLevel.Error, StatusCodes.Status500InternalServerError)
    };
}
