using System.Text.RegularExpressions;
using Serilog.Context;

namespace IngestionApi.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private static readonly Regex CorrelationIdPattern = new(@"^[A-Za-z0-9\-]{1,36}$", RegexOptions.Compiled);
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    private static bool IsValidCorrelationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 36)
        {
            return false;
        }

        // Accept if it matches the alphanumeric+hyphen pattern
        if (CorrelationIdPattern.IsMatch(value))
        {
            return true;
        }

        // Accept if it parses as a valid GUID
        return Guid.TryParse(value, out _);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var value)
            && IsValidCorrelationId(value.ToString())
            ? value.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
