# Vendor Signature Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Validate an `X-Vendor-Signature` RSA-SHA256 header on all `VendorTransactionsController` endpoints, using per-vendor certificates loaded from the Windows Certificate Store, with a per-vendor toggle to bypass validation.

**Architecture:** An `IAsyncActionFilter` (`VendorSignatureFilter`) is applied at the controller class level. Certificate loading is abstracted behind `ICertificateLoader` (implemented by `WindowsCertificateLoader`) to allow future Linux migration. Vendor thumbprints and the verification toggle live in `appsettings.json` under `VendorCertificates`, bound via `IOptions<VendorCertificateOptions>`.

**Tech Stack:** .NET 10 / ASP.NET Core, `System.Security.Cryptography`, `System.Security.Cryptography.X509Certificates`, Moq (unit tests), `WebApplicationFactory` (integration tests)

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `src/IngestionApi.Api/Options/VendorCertificateOptions.cs` | Create | Strongly-typed config; `Dictionary<string, VendorCertificateEntry>` |
| `src/IngestionApi.Api/Filters/ICertificateLoader.cs` | Create | Abstraction: `X509Certificate2? Load(string thumbprint)` |
| `src/IngestionApi.Api/Filters/WindowsCertificateLoader.cs` | Create | Windows cert store implementation of `ICertificateLoader` |
| `src/IngestionApi.Api/Filters/VendorSignatureFilter.cs` | Create | `IAsyncActionFilter` — validates `X-Vendor-Signature` |
| `src/IngestionApi.Api/Program.cs` | Modify | Register options, `ICertificateLoader`, `VendorSignatureFilter` |
| `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs` | Modify | Apply `[ServiceFilter(typeof(VendorSignatureFilter))]` |
| `src/IngestionApi.Api/appsettings.json` | Modify | Add `VendorCertificates` section |
| `tests/IngestionApi.Tests.Unit/IngestionApi.Tests.Unit.csproj` | Modify | Add ProjectReference to Api project |
| `tests/IngestionApi.Tests.Unit/Filters/VendorSignatureFilterTests.cs` | Create | Unit tests for the filter |
| `tests/IngestionApi.Tests.Integration/Filters/VendorSignatureIntegrationTests.cs` | Create | Integration tests with real RSA key pair |

---

## Task 1: Create feature branch

- [ ] **Step 1: Create and switch to feature branch**

```bash
git checkout -b feature/vendor-signature
```

Expected: `Switched to a new branch 'feature/vendor-signature'`

---

## Task 2: VendorCertificateOptions

**Files:**
- Create: `src/IngestionApi.Api/Options/VendorCertificateOptions.cs`
- Modify: `src/IngestionApi.Api/appsettings.json`

- [ ] **Step 1: Create the options classes**

Create `src/IngestionApi.Api/Options/VendorCertificateOptions.cs`:

```csharp
namespace IngestionApi.Api.Options;

public class VendorCertificateOptions
{
    public Dictionary<string, VendorCertificateEntry> Vendors { get; set; } = new();
}

public class VendorCertificateEntry
{
    public string Thumbprint { get; set; } = string.Empty;
    public bool SignatureVerificationEnabled { get; set; }
}
```

- [ ] **Step 2: Add the config section to appsettings.json**

Read `src/IngestionApi.Api/appsettings.json` first, then add the `VendorCertificates` section:

```json
"VendorCertificates": {
  "Vendors": {
    "vendor-001": {
      "Thumbprint": "",
      "SignatureVerificationEnabled": false
    }
  }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Api/Options/VendorCertificateOptions.cs src/IngestionApi.Api/appsettings.json
git commit -m "feat: add VendorCertificateOptions"
```

---

## Task 3: ICertificateLoader and WindowsCertificateLoader

**Files:**
- Create: `src/IngestionApi.Api/Filters/ICertificateLoader.cs`
- Create: `src/IngestionApi.Api/Filters/WindowsCertificateLoader.cs`

- [ ] **Step 1: Create the interface**

Create `src/IngestionApi.Api/Filters/ICertificateLoader.cs`:

```csharp
using System.Security.Cryptography.X509Certificates;

namespace IngestionApi.Api.Filters;

public interface ICertificateLoader
{
    X509Certificate2? Load(string thumbprint);
}
```

- [ ] **Step 2: Create the Windows implementation**

Create `src/IngestionApi.Api/Filters/WindowsCertificateLoader.cs`:

```csharp
using System.Security.Cryptography.X509Certificates;

namespace IngestionApi.Api.Filters;

public class WindowsCertificateLoader : ICertificateLoader
{
    public X509Certificate2? Load(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        var results = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        return results.Count > 0 ? results[0] : null;
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Api/Filters/ICertificateLoader.cs src/IngestionApi.Api/Filters/WindowsCertificateLoader.cs
git commit -m "feat: add ICertificateLoader and WindowsCertificateLoader"
```

---

## Task 4: VendorSignatureFilter

**Files:**
- Create: `src/IngestionApi.Api/Filters/VendorSignatureFilter.cs`

- [ ] **Step 1: Create the filter**

Create `src/IngestionApi.Api/Filters/VendorSignatureFilter.cs`:

```csharp
using IngestionApi.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace IngestionApi.Api.Filters;

public class VendorSignatureFilter : IAsyncActionFilter
{
    private readonly VendorCertificateOptions _options;
    private readonly ICertificateLoader _certificateLoader;
    private readonly ILogger<VendorSignatureFilter> _logger;

    public VendorSignatureFilter(
        IOptions<VendorCertificateOptions> options,
        ICertificateLoader certificateLoader,
        ILogger<VendorSignatureFilter> logger)
    {
        _options = options.Value;
        _certificateLoader = certificateLoader;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var vendorId = request.Headers["X-Vendor-Id"].ToString();
        var signature = request.Headers["X-Vendor-Signature"].ToString();

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Missing X-Vendor-Signature header for vendor {VendorId}", vendorId);
            context.Result = new UnauthorizedObjectResult(new { detail = "X-Vendor-Signature header is required" });
            return;
        }

        if (!_options.Vendors.TryGetValue(vendorId, out var entry))
        {
            _logger.LogWarning("Unknown vendor {VendorId}", vendorId);
            context.Result = new UnauthorizedObjectResult(new { detail = "Unknown vendor" });
            return;
        }

        if (!entry.SignatureVerificationEnabled)
        {
            await next();
            return;
        }

        try
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
            var signatureBytes = Convert.FromBase64String(signature);

            var certificate = _certificateLoader.Load(entry.Thumbprint);
            if (certificate is null)
            {
                _logger.LogWarning("Certificate not found for vendor {VendorId} thumbprint {Thumbprint}", vendorId, entry.Thumbprint);
                context.Result = new UnauthorizedObjectResult(new { detail = "Invalid signature" });
                return;
            }

            using var rsa = certificate.GetRSAPublicKey()
                ?? throw new InvalidOperationException("Certificate has no RSA public key.");

            if (!rsa.VerifyData(bodyBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                _logger.LogWarning("Invalid signature for vendor {VendorId}", vendorId);
                context.Result = new UnauthorizedObjectResult(new { detail = "Invalid signature" });
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Signature verification failed for vendor {VendorId}", vendorId);
            context.Result = new UnauthorizedObjectResult(new { detail = "Invalid signature" });
            return;
        }

        await next();
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/IngestionApi.Api/Filters/VendorSignatureFilter.cs
git commit -m "feat: add VendorSignatureFilter"
```

---

## Task 5: Wire up in Program.cs and VendorTransactionsController

**Files:**
- Modify: `src/IngestionApi.Api/Program.cs`
- Modify: `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs`

- [ ] **Step 1: Register services in Program.cs**

In `src/IngestionApi.Api/Program.cs`, add these three lines after `builder.Services.AddInfrastructure(builder.Configuration);`:

```csharp
builder.Services.Configure<VendorCertificateOptions>(
    builder.Configuration.GetSection("VendorCertificates"));
builder.Services.AddSingleton<ICertificateLoader, WindowsCertificateLoader>();
builder.Services.AddScoped<VendorSignatureFilter>();
```

Also add the required usings at the top of `Program.cs`:

```csharp
using IngestionApi.Api.Filters;
using IngestionApi.Api.Options;
```

- [ ] **Step 2: Apply filter to VendorTransactionsController**

In `src/IngestionApi.Api/Controllers/VendorTransactionsController.cs`, add the attribute above the class declaration:

```csharp
[ServiceFilter(typeof(VendorSignatureFilter))]
[ApiController]
[Route("api/vendor-transactions")]
public class VendorTransactionsController : ControllerBase
```

- [ ] **Step 3: Build**

```bash
dotnet build IngestionApi.slnx
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/IngestionApi.Api/Program.cs src/IngestionApi.Api/Controllers/VendorTransactionsController.cs
git commit -m "feat: wire up VendorSignatureFilter in Program.cs and VendorTransactionsController"
```

---

## Task 6: Unit tests for VendorSignatureFilter

**Files:**
- Modify: `tests/IngestionApi.Tests.Unit/IngestionApi.Tests.Unit.csproj`
- Create: `tests/IngestionApi.Tests.Unit/Filters/VendorSignatureFilterTests.cs`

- [ ] **Step 1: Add Api project reference to unit test csproj**

In `tests/IngestionApi.Tests.Unit/IngestionApi.Tests.Unit.csproj`, add inside the existing `<ItemGroup>` that contains project references:

```xml
<ProjectReference Include="..\..\src\IngestionApi.Api\IngestionApi.Api.csproj" />
```

- [ ] **Step 2: Write the unit tests**

Create `tests/IngestionApi.Tests.Unit/Filters/VendorSignatureFilterTests.cs`:

```csharp
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

    private static (ActionExecutingContext context, bool nextCalled) CreateContext(
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
        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);

        return (executingContext, false);
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
        var (context, _) = CreateContext(signature: null);
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task UnknownVendor_Returns401()
    {
        var filter = CreateFilter(new Dictionary<string, VendorCertificateEntry>());
        var (context, _) = CreateContext(signature: "abc");
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
        var (context, _) = CreateContext(signature: "whatever");
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
        var (context, _) = CreateContext(signature: Sign(body, privateKey), body: body);
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

        var (context, _) = CreateContext(signature: Convert.ToBase64String(new byte[256]), body: "{\"test\":1}");
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

        var (context, _) = CreateContext(signature: Convert.ToBase64String(new byte[32]));
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(context, () => { nextCalled = true; return Task.FromResult<ActionExecutedContext>(null!); });

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.False(nextCalled);
    }
}
```

- [ ] **Step 3: Run the tests to verify they pass**

```bash
dotnet test tests/IngestionApi.Tests.Unit/ --filter "FullyQualifiedName~VendorSignatureFilterTests" -v
```

Expected: 6 tests, all **PASS**

- [ ] **Step 4: Commit**

```bash
git add tests/IngestionApi.Tests.Unit/IngestionApi.Tests.Unit.csproj tests/IngestionApi.Tests.Unit/Filters/VendorSignatureFilterTests.cs
git commit -m "test: add VendorSignatureFilter unit tests"
```

---

## Task 7: Integration tests

**Files:**
- Create: `tests/IngestionApi.Tests.Integration/Filters/VendorSignatureIntegrationTests.cs`

- [ ] **Step 1: Write the integration tests**

Create `tests/IngestionApi.Tests.Integration/Filters/VendorSignatureIntegrationTests.cs`:

```csharp
using IngestionApi.Api.Filters;
using IngestionApi.Api.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace IngestionApi.Tests.Integration.Filters;

public class VendorSignatureIntegrationTests
{
    private const string VendorId = "test-vendor";
    private const string Thumbprint = "TEST-THUMBPRINT";

    private static readonly string ValidBody = JsonSerializer.Serialize(new
    {
        operationId = "op-001",
        bagId = "bag-001",
        bags = new[]
        {
            new { declaredValue = 100m, processedValue = 100m, differenceValue = 0m, currency = "USD" }
        }
    });

    private static X509Certificate2 CreateTestCertificate(out RSA privateKey)
    {
        privateKey = RSA.Create(2048);
        var req = new CertificateRequest("cn=test-vendor", privateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
    }

    private static string Sign(string body, RSA privateKey) =>
        Convert.ToBase64String(
            privateKey.SignData(Encoding.UTF8.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    private static WebApplicationFactory<Program> CreateFactory(X509Certificate2 cert)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.Sources.Clear();
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=(localdb)\\mssqllocaldb;Database=IngestionDb;Trusted_Connection=True;TrustServerCertificate=True;",
                    ["VendorCertificates:Vendors:test-vendor:Thumbprint"] = Thumbprint,
                    ["VendorCertificates:Vendors:test-vendor:SignatureVerificationEnabled"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove WindowsCertificateLoader, replace with stub
                var descriptor = services.Single(d => d.ServiceType == typeof(ICertificateLoader));
                services.Remove(descriptor);
                services.AddSingleton<ICertificateLoader>(new StubCertificateLoader(Thumbprint, cert));
            });
        });
    }

    [Fact]
    public async Task ValidSignature_ReturnsAccepted()
    {
        var testCert = CreateTestCertificate(out var privateKey);
        await using var factory = CreateFactory(testCert);
        var client = factory.CreateClient();

        var signature = Sign(ValidBody, privateKey);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vendor-transactions/T");
        request.Content = new StringContent(ValidBody, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Vendor-Id", VendorId);
        request.Headers.Add("X-Vendor-Signature", signature);

        var response = await client.SendAsync(request);

        // 202 Accepted or 409 Conflict (duplicate) — signature passed either way
        Assert.True(
            response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.Conflict,
            $"Expected 202 or 409 but got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task TamperedBody_Returns401()
    {
        var testCert = CreateTestCertificate(out var privateKey);
        await using var factory = CreateFactory(testCert);
        var client = factory.CreateClient();

        // Sign original body, then send a different body
        var signature = Sign(ValidBody, privateKey);
        var tamperedBody = ValidBody.Replace("op-001", "op-TAMPERED");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vendor-transactions/T");
        request.Content = new StringContent(tamperedBody, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Vendor-Id", VendorId);
        request.Headers.Add("X-Vendor-Signature", signature);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingSignatureHeader_Returns401()
    {
        var testCert = CreateTestCertificate(out _);
        await using var factory = CreateFactory(testCert);
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vendor-transactions/T");
        request.Content = new StringContent(ValidBody, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Vendor-Id", VendorId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class StubCertificateLoader : ICertificateLoader
    {
        private readonly string _thumbprint;
        private readonly X509Certificate2 _cert;

        public StubCertificateLoader(string thumbprint, X509Certificate2 cert)
        {
            _thumbprint = thumbprint;
            _cert = cert;
        }

        public X509Certificate2? Load(string thumbprint) =>
            thumbprint == _thumbprint ? _cert : null;
    }
}
```

- [ ] **Step 2: Run the integration tests**

```bash
dotnet test tests/IngestionApi.Tests.Integration/ --filter "FullyQualifiedName~VendorSignatureIntegrationTests" -v
```

Expected: 3 tests, all **PASS**

- [ ] **Step 3: Run the full test suite**

```bash
dotnet test IngestionApi.slnx
```

Expected: all tests pass, 0 failures.

- [ ] **Step 4: Commit**

```bash
git add tests/IngestionApi.Tests.Integration/Filters/VendorSignatureIntegrationTests.cs
git commit -m "test: add vendor signature integration tests"
```
