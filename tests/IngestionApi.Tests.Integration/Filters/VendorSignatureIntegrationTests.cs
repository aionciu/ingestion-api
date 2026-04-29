using IngestionApi.Api.Filters;
using IngestionApi.Api.Options;
using IngestionApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
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
            new { declaredValue = 100m, processedValue = 0m, differenceValue = 0m, currency = "USD" }
        }
    });

    private static X509Certificate2 CreateTestCertificate()
    {
        using var key = RSA.Create(2048);
        var req = new CertificateRequest("cn=test-vendor", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
    }

    private static string Sign(string body, X509Certificate2 cert)
    {
        using var privateKey = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Certificate has no RSA private key.");
        return Convert.ToBase64String(
            privateKey.SignData(Encoding.UTF8.GetBytes(body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

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
                // Replace SQL Server DbContext with in-memory database
                var dbContextOptions = services.Where(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)).ToList();
                var dbContextServices = services.Where(
                    d => d.ServiceType == typeof(DbContext) || d.ServiceType == typeof(AppDbContext)).ToList();
                foreach (var d in dbContextOptions.Concat(dbContextServices))
                    services.Remove(d);

                var efCoreServices = services.Where(
                    d => d.ServiceType?.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true ||
                         d.ImplementationType?.FullName?.StartsWith("Microsoft.EntityFrameworkCore") == true).ToList();
                foreach (var d in efCoreServices)
                    services.Remove(d);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("VendorSignatureTestDb"));

                // Replace ICertificateLoader with stub
                var certLoaderDescriptor = services.Single(d => d.ServiceType == typeof(ICertificateLoader));
                services.Remove(certLoaderDescriptor);
                services.AddSingleton<ICertificateLoader>(new StubCertificateLoader(Thumbprint, cert));

                // Ensure schema is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    [Fact]
    public async Task ValidSignature_ReturnsAccepted()
    {
        var testCert = CreateTestCertificate();
        await using var factory = CreateFactory(testCert);
        var client = factory.CreateClient();

        var signature = Sign(ValidBody, testCert);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vendor-transactions/T");
        request.Content = new StringContent(ValidBody, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Vendor-Id", VendorId);
        request.Headers.Add("X-Vendor-Signature", signature);

        var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        // 202 Accepted or 409 Conflict (duplicate op) — signature passed either way
        Assert.True(
            response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.Conflict,
            $"Expected 202 or 409 but got {(int)response.StatusCode}: {body}");
    }

    [Fact]
    public async Task TamperedBody_Returns401()
    {
        var testCert = CreateTestCertificate();
        await using var factory = CreateFactory(testCert);
        var client = factory.CreateClient();

        var signature = Sign(ValidBody, testCert);
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
        var testCert = CreateTestCertificate();
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
