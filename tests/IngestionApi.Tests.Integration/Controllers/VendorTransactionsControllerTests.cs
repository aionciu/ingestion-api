using IngestionApi.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using Xunit;

namespace IngestionApi.Tests.Integration.Controllers;

public class VendorTransactionsControllerTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    public VendorTransactionsControllerTests()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task PostT_ValidRequest_Returns202Accepted()
    {
        var request = new
        {
            operationId = "OP001",
            bagId = "BAG001",
            bags = new[] { new { declaredValue = 1000m, processedValue = 0m, differenceValue = 0m, currency = "USD" } }
        };

        _client.DefaultRequestHeaders.Add("X-Vendor-Id", "V001");

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/vendor-transactions/T", content);

        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostT1_ValidRequest_Returns202Accepted()
    {
        var request = new
        {
            operationId = "OP002",
            bagId = "BAG002",
            bags = new[] { new { declaredValue = 1000m, processedValue = 500m, differenceValue = 100m, currency = "EUR" } }
        };

        _client.DefaultRequestHeaders.Add("X-Vendor-Id", "V002");

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/vendor-transactions/T+1", content);

        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostT_MissingVendorIdHeader_Returns400()
    {
        var request = new
        {
            operationId = "OP003",
            bagId = "BAG003",
            bags = new[] { new { declaredValue = 1000m, processedValue = 0m, differenceValue = 0m, currency = "USD" } }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/vendor-transactions/T", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostT_InvalidValidation_ProcessedValueNonZero_Returns400()
    {
        var request = new
        {
            operationId = "OP004",
            bagId = "BAG004",
            bags = new[] { new { declaredValue = 1000m, processedValue = 100m, differenceValue = 0m, currency = "USD" } }
        };

        _client.DefaultRequestHeaders.Add("X-Vendor-Id", "V004");

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/vendor-transactions/T", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostT_InvalidValidation_DeclaredValueZero_Returns400()
    {
        var request = new
        {
            operationId = "OP005",
            bagId = "BAG005",
            bags = new[] { new { declaredValue = 0m, processedValue = 0m, differenceValue = 0m, currency = "USD" } }
        };

        _client.DefaultRequestHeaders.Add("X-Vendor-Id", "V005");

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/vendor-transactions/T", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
