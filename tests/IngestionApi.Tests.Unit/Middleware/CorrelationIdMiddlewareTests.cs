using IngestionApi.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace IngestionApi.Tests.Unit.Middleware;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task NoHeader_GeneratesId()
    {
        var ctx = new DefaultHttpContext();
        var mw = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await mw.InvokeAsync(ctx);

        var id = ctx.Items["CorrelationId"] as string;
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(id, ctx.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task ValidHeader_PropagatesId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Correlation-Id"] = "abc-123";
        var mw = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await mw.InvokeAsync(ctx);

        Assert.Equal("abc-123", ctx.Items["CorrelationId"]);
        Assert.Equal("abc-123", ctx.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task ValidGuidHeader_Propagates()
    {
        var ctx = new DefaultHttpContext();
        var g = Guid.NewGuid().ToString();
        ctx.Request.Headers["X-Correlation-Id"] = g;
        var mw = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await mw.InvokeAsync(ctx);

        Assert.Equal(g, ctx.Items["CorrelationId"]);
    }

    [Fact]
    public async Task InvalidHeader_GeneratesNewId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Correlation-Id"] = "bad value with spaces and *";
        var mw = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await mw.InvokeAsync(ctx);

        var id = ctx.Items["CorrelationId"] as string;
        Assert.NotEqual("bad value with spaces and *", id);
        Assert.True(Guid.TryParse(id, out _));
    }

    [Fact]
    public async Task EmptyHeader_GeneratesNewId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Correlation-Id"] = "";
        var mw = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await mw.InvokeAsync(ctx);

        Assert.True(Guid.TryParse(ctx.Items["CorrelationId"] as string, out _));
    }

    [Fact]
    public async Task TooLongHeader_GeneratesNewId()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Correlation-Id"] = new string('a', 50);
        var mw = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        await mw.InvokeAsync(ctx);

        Assert.True(Guid.TryParse(ctx.Items["CorrelationId"] as string, out _));
    }
}
