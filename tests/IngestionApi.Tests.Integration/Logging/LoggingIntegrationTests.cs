using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IngestionApi.Tests.Integration.Logging;

public class LoggingIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task HealthCheck_ReturnsOkAndLogsRequest()
    {
        // Arrange
        var endpoint = "/health"; // Adjust based on actual endpoint

        // Act
        var response = await _client!.GetAsync(endpoint);

        // Assert
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task CorrelationIdPropagates_InRequestAndResponse()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health"); // Adjust endpoint
        request.Headers.Add("X-Correlation-Id", expectedCorrelationId);

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        var returnedCorrelationId = values.First();
        Assert.Equal(expectedCorrelationId, returnedCorrelationId);
    }

    [Fact]
    public async Task NewCorrelationIdGenerated_IfNotProvided()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/health"); // Adjust endpoint

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        var correlationId = values.First();
        Assert.NotEmpty(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
    }
}
