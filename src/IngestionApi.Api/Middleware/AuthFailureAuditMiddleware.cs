using System.Text;

namespace IngestionApi.Api.Middleware;

public class AuthFailureAuditMiddleware
{
    internal const int MaxBodyBytes = 8192;
    private const string SignatureHeader = "X-Vendor-Signature";
    private const string JsonContentTypePrefix = "application/json";
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Proxy-Authorization",
        "X-Forwarded-Authorization",
        "X-Original-Authorization",
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
        var (body, bodyTruncated, bodyTotalBytes) = await ReadBodyAsync(request);

        _logger.LogWarning(
            "401 audit: {Method} {Path}{QueryString} from {RemoteIp} headers={@Headers} body={Body} truncated={BodyTruncated} totalBytes={BodyTotalBytes}",
            method, path, queryString, remoteIp, headers, body, bodyTruncated, bodyTotalBytes);
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

    private static async Task<(string Body, bool Truncated, long TotalBytes)> ReadBodyAsync(HttpRequest request)
    {
        var contentLength = request.ContentLength ?? 0;
        if (contentLength == 0)
        {
            return (string.Empty, false, 0);
        }

        var contentType = request.ContentType ?? string.Empty;
        if (!contentType.StartsWith(JsonContentTypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ("[non-json content omitted]", false, contentLength);
        }

        request.Body.Position = 0;

        var buffer = new byte[MaxBodyBytes];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        var truncated = contentLength > MaxBodyBytes;
        var totalBytes = truncated ? contentLength : read;
        // Byte-based truncation may split a multi-byte UTF-8 sequence at the cap; replacement char acceptable for diagnostics.
        var body = Encoding.UTF8.GetString(buffer, 0, read);

        request.Body.Position = 0;

        return (body, truncated, totalBytes);
    }
}
