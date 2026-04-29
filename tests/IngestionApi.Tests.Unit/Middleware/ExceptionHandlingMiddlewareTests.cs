using FluentValidation;
using IngestionApi.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngestionApi.Tests.Unit.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static async Task<HttpContext> RunWith(Exception ex)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Items["CorrelationId"] = "cid";
        var mw = new ExceptionHandlingMiddleware(_ => throw ex, NullLogger<ExceptionHandlingMiddleware>.Instance);
        await mw.InvokeAsync(ctx);
        return ctx;
    }

    [Fact]
    public async Task NoException_PassesThrough()
    {
        var ctx = new DefaultHttpContext();
        bool called = false;
        var mw = new ExceptionHandlingMiddleware(_ => { called = true; return Task.CompletedTask; }, NullLogger<ExceptionHandlingMiddleware>.Instance);
        await mw.InvokeAsync(ctx);
        Assert.True(called);
    }

    [Fact]
    public async Task InvalidOperation_Returns500() =>
        Assert.Equal(500, (await RunWith(new InvalidOperationException("x"))).Response.StatusCode);

    [Fact]
    public async Task NullReference_Returns500() =>
        Assert.Equal(500, (await RunWith(new NullReferenceException("x"))).Response.StatusCode);

    [Fact]
    public async Task ValidationException_Returns422() =>
        Assert.Equal(422, (await RunWith(new ValidationException("x"))).Response.StatusCode);

    [Fact]
    public async Task ArgumentException_Returns400() =>
        Assert.Equal(400, (await RunWith(new ArgumentException("x"))).Response.StatusCode);

    [Fact]
    public async Task TimeoutException_Returns503() =>
        Assert.Equal(503, (await RunWith(new TimeoutException("x"))).Response.StatusCode);

    [Fact]
    public async Task IOException_Returns503() =>
        Assert.Equal(503, (await RunWith(new IOException("x"))).Response.StatusCode);

    [Fact]
    public async Task UnknownException_Returns500() =>
        Assert.Equal(500, (await RunWith(new ApplicationException("x"))).Response.StatusCode);
}
