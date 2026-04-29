using IngestionApi.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngestionApi.Tests.Unit.Middleware;

public class RequestResponseLoggingMiddlewareTests
{
    private static async Task<HttpContext> Run(int status)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/x";
        var mw = new RequestResponseLoggingMiddleware(c =>
        {
            c.Response.StatusCode = status;
            return Task.CompletedTask;
        }, NullLogger<RequestResponseLoggingMiddleware>.Instance);
        await mw.InvokeAsync(ctx);
        return ctx;
    }

    [Fact]
    public async Task Success_LogsInformation()
    {
        var ctx = await Run(200);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ClientError_LogsWarning()
    {
        var ctx = await Run(404);
        Assert.Equal(404, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ServerError_LogsError()
    {
        var ctx = await Run(500);
        Assert.Equal(500, ctx.Response.StatusCode);
    }
}
