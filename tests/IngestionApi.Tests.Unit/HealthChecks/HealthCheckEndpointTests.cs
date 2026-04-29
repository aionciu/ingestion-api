using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace IngestionApi.Tests.Unit.HealthChecks;

public class HealthCheckEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task LiveEndpoint_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyEndpoint_ReturnsHealthyOrUnhealthyDependingOnDb()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        // 200 when DB is reachable; 503 when DB is unavailable (e.g. in CI)
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503 but got {(int)response.StatusCode}");
    }
}
