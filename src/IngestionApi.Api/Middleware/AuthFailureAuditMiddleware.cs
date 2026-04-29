namespace IngestionApi.Api.Middleware;

public class AuthFailureAuditMiddleware
{
    private const string SignatureHeader = "X-Vendor-Signature";
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Proxy-Authorization",
    };

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

        var request = context.Request;
        var method = request.Method;
        var path = request.Path.Value ?? string.Empty;
        var queryString = request.QueryString.Value ?? string.Empty;
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var headers = RedactHeaders(request.Headers);

        _logger.LogWarning(
            "401 audit: {Method} {Path}{QueryString} from {RemoteIp} headers={@Headers}",
            method, path, queryString, remoteIp, headers);
    }

    private static IDictionary<string, string> RedactHeaders(IHeaderDictionary source)
    {
        var result = new Dictionary<string, string>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in source)
        {
            if (SensitiveHeaders.Contains(name))
            {
                result[name] = "***";
                continue;
            }

            var raw = string.Join(", ", values.ToArray());

            if (string.Equals(name, SignatureHeader, StringComparison.OrdinalIgnoreCase) && raw.Length > 8)
            {
                result[name] = $"{raw[..8]}…(len={raw.Length})";
                continue;
            }

            result[name] = raw;
        }
        return result;
    }
}
