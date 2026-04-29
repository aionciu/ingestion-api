using IngestionApi.Api.Filters;
using IngestionApi.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace IngestionApi.Tests.Unit.Filters;

public class VendorSignatureFilterTests
{
    private const string VendorId = "vendor-001";
    private const string Thumbprint = "AABBCCDD";

    private static VendorSignatureFilter CreateFilter(
        Dictionary<string, VendorCertificateEntry> vendors,
        ICertificateLoader? loader = null)
    {
        var options = Options.Create(new VendorCertificateOptions { Vendors = vendors });
        loader ??= new Mock<ICertificateLoader>().Object;
        return new VendorSignatureFilter(options, loader, NullLogger<VendorSignatureFilter>.Instance);
    }

    private static ActionExecutingContext CreateContext(
        string? vendorId = VendorId,
        string? signature = null,
        string body = "{}")
    {
        var httpContext = new DefaultHttpContext();
        if (vendorId is not null)
            httpContext.Request.Headers["X-Vendor-Id"] = vendorId;
        if (signature is not null)
            httpContext.Request.Headers["X-Vendor-Signature"] = signature;

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        httpContext.Request.ContentType = "application/json";

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }

    private static X509Certificate2 CreateTestCertificate(out RSA privateKey)
    {
        privateKey = RSA.Create(2048);
        var req = new CertificateRequest("cn=test", privateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
    }

    private static string Sign(string body, RSA privateKey)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        return Convert.ToBase64String(privateKey.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public async Task MissingSignatureHeader_Returns401()
    {
        var filter = CreateFilter(new Dictionary<string, VendorCertificateEntry>
        {
            [VendorId] = new() { Thumbprint = Thumbprint, SignatureVerificationEnabled = true }
        });
        var context = CreateContext(signature: null);
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task UnknownVendor_Returns401()
    {
        var filter = CreateFilter(new Dictionary<string, VendorCertificateEntry>());
        var context = CreateContext(signature: "abc");
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task VerificationDisabled_PassesThrough()
    {
        var filter = CreateFilter(new Dictionary<string, VendorCertificateEntry>
        {
            [VendorId] = new() { Thumbprint = Thumbprint, SignatureVerificationEnabled = false }
        });
        var context = CreateContext(signature: "whatever");
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.Null(context.Result);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task ValidSignature_PassesThrough()
    {
        var cert = CreateTestCertificate(out var privateKey);
        var loaderMock = new Mock<ICertificateLoader>();
        loaderMock.Setup(l => l.Load(Thumbprint)).Returns(cert);

        var filter = CreateFilter(new Dictionary<string, VendorCertificateEntry>
        {
            [VendorId] = new() { Thumbprint = Thumbprint, SignatureVerificationEnabled = true }
        }, loaderMock.Object);

        const string body = "{\"test\":1}";
        var context = CreateContext(signature: Sign(body, privateKey), body: body);
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.Null(context.Result);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvalidSignature_Returns401()
    {
        var cert = CreateTestCertificate(out _);
        var loaderMock = new Mock<ICertificateLoader>();
        loaderMock.Setup(l => l.Load(Thumbprint)).Returns(cert);

        var filter = CreateFilter(new Dictionary<string, VendorCertificateEntry>
        {
            [VendorId] = new() { Thumbprint = Thumbprint, SignatureVerificationEnabled = true }
        }, loaderMock.Object);

        var context = CreateContext(signature: Convert.ToBase64String(new byte[256]), body: "{\"test\":1}");
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task CertificateNotFound_Returns401()
    {
        var loaderMock = new Mock<ICertificateLoader>();
        loaderMock.Setup(l => l.Load(Thumbprint)).Returns((X509Certificate2?)null);

        var filter = CreateFilter(new Dictionary<string, VendorCertificateEntry>
        {
            [VendorId] = new() { Thumbprint = Thumbprint, SignatureVerificationEnabled = true }
        }, loaderMock.Object);

        var context = CreateContext(signature: Convert.ToBase64String(new byte[32]));
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.False(nextCalled);
    }
}
