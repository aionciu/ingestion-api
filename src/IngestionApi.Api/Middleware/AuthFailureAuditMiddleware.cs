namespace IngestionApi.Api.Middleware;

public class AuthFailureAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthFailureAuditMiddleware> _logger;

    public AuthFailureAuditMiddleware(RequestDelegate next, ILogger<AuthFailureAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        await _next(context);

        if (context.Response.StatusCode != StatusCodes.Status401Unauthorized)
        {
            return;
        }
    }
}
