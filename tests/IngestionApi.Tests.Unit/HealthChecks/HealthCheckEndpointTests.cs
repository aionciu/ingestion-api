using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

namespace IngestionApi.Tests.Unit.HealthChecks;

public class HealthCheckEndpointTests
{
    [Fact]
    public async Task LiveEndpoint_ReturnsOk()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            "Server=(localdb)\\mssqllocaldb;Database=IngestionDb;Trusted_Connection=True;TrustServerCertificate=True;"
                    });
                });
            });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyEndpoint_ReturnsServiceUnavailableWhenDbUnreachable()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            "Server=(localdb)\\mssqllocaldb;Database=IngestionDb;Trusted_Connection=True;TrustServerCertificate=True;"
                    });
                });

                // Replace all health checks with one that always reports Unhealthy for "ready"
                builder.ConfigureServices(services =>
                {
                    // Remove existing health check registrations
                    var descriptors = services
                        .Where(d => d.ServiceType == typeof(IHealthCheck) ||
                                    d.ServiceType.FullName?.Contains("HealthCheck") == true)
                        .ToList();

                    services.AddHealthChecks()
                        .AddCheck("sql", () => HealthCheckResult.Unhealthy("DB unreachable"), tags: ["ready"]);
                });
            });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        // Unhealthy check tagged "ready" → 503
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
