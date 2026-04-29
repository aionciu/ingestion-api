using IngestionApi.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Tests.Unit.Middleware;

public class AuthFailureAuditMiddlewareTests
{
    private sealed class CapturingLogger : ILogger<AuthFailureAuditMiddleware>
    {
        public List<(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State)> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var kvps = state as IReadOnlyList<KeyValuePair<string, object?>> ?? Array.Empty<KeyValuePair<string, object?>>();
            Entries.Add((logLevel, formatter(state, exception), kvps));
        }
    }

    private static async Task<(HttpContext Ctx, CapturingLogger Log)> Run(
        int status,
        Action<HttpContext>? configureRequest = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/api/x";
        configureRequest?.Invoke(ctx);

        var logger = new CapturingLogger();
        var mw = new AuthFailureAuditMiddleware(c =>
        {
            c.Response.StatusCode = status;
            return Task.CompletedTask;
        }, logger);
        await mw.InvokeAsync(ctx);
        return (ctx, logger);
    }

    [Fact]
    public async Task NonUnauthorized_EmitsNoAuditRecord()
    {
        var (_, log) = await Run(200);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task Unauthorized_EmitsWarningWithEnvelopeFields()
    {
        var (_, log) = await Run(401, ctx =>
        {
            ctx.Request.QueryString = new QueryString("?a=1");
            ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        });

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);

        var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal("POST", dict["Method"]);
        Assert.Equal("/api/x", dict["Path"]);
        Assert.Equal("?a=1", dict["QueryString"]);
        Assert.Equal("10.0.0.5", dict["RemoteIp"]);
    }

    [Fact]
    public async Task Unauthorized_RedactsSensitiveHeaders()
    {
        var (_, log) = await Run(401, ctx =>
        {
            ctx.Request.Headers["Authorization"] = "Bearer secret";
            ctx.Request.Headers["Cookie"] = "session=abc";
            ctx.Request.Headers["X-Vendor-Id"] = "vendor-001";
        });

        var entry = Assert.Single(log.Entries);
        var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var headers = Assert.IsAssignableFrom<IDictionary<string, string>>(dict["@Headers"]);

        Assert.Equal("***", headers["Authorization"]);
        Assert.Equal("***", headers["Cookie"]);
        Assert.Equal("vendor-001", headers["X-Vendor-Id"]);
    }

    [Fact]
    public async Task Unauthorized_TruncatesVendorSignature()
    {
        var sig = new string('A', 200);
        var (_, log) = await Run(401, ctx =>
        {
            ctx.Request.Headers["X-Vendor-Signature"] = sig;
        });

        var entry = Assert.Single(log.Entries);
        var dict = entry.State.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var headers = (IDictionary<string, string>)dict["@Headers"]!;

        Assert.Equal("AAAAAAAA…(len=200)", headers["X-Vendor-Signature"]);
    }
}
