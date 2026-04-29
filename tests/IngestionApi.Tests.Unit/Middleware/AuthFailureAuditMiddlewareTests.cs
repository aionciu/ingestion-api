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
}
