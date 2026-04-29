using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

namespace IngestionApi.Tests.Integration.HealthChecks;

public class HealthCheckEndpointTests
{
    [Fact]
    public async Task LiveEndpoint_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>()
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
        await using var factory = new WebApplicationFactory<Program>()
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

                builder.ConfigureServices(services =>
                {
                    services.AddHealthChecks()
                        .AddCheck("sql", () => HealthCheckResult.Unhealthy("DB unreachable"), tags: ["ready"]);
                });
            });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
