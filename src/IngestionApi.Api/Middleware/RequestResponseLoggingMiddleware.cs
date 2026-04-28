using System.Diagnostics;

namespace IngestionApi.Api.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;

        _logger.LogInformation("{Method} {Path} started", method, path);

        // For transaction endpoints, capture request body for audit
        if (IsTransactionEndpoint(path))
        {
            await CaptureRequestBody(context);
        }

        var stopwatch = Stopwatch.StartNew();
        await _next(context);
        stopwatch.Stop();

        var statusCode = context.Response.StatusCode;
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        var logLevel = statusCode >= 500 ? LogLevel.Error
                     : statusCode >= 400 ? LogLevel.Warning
                     : LogLevel.Information;

        _logger.Log(logLevel, "{Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
            method, path, elapsedMs, statusCode);
    }

    private static bool IsTransactionEndpoint(PathString path)
    {
        return path.StartsWithSegments("/api/transactions", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CaptureRequestBody(HttpContext context)
    {
        context.Request.EnableBuffering();
        var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        var logger = context.RequestServices.GetRequiredService<ILogger<RequestResponseLoggingMiddleware>>();
        logger.LogInformation("Transaction payload received: {Payload}", body);
    }
}
