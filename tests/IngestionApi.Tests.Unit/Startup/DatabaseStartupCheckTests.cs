using IngestionApi.Api.Startup;
using IngestionApi.Infrastructure.Persistence;
using IngestionApi.Tests.Unit.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngestionApi.Tests.Unit.Startup;

public class DatabaseStartupCheckTests
{
    [Fact]
    public async Task Start_CanConnect_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => InMemoryDbContextFactory.Create("startup-ok"));
        var sp = services.BuildServiceProvider();

        var check = new DatabaseStartupCheck(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<DatabaseStartupCheck>.Instance);
        await check.StartAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Start_ThrowingProvider_WrapsAsInvalidOperation()
    {
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => throw new InvalidOperationException("boom"));
        var sp = services.BuildServiceProvider();

        var check = new DatabaseStartupCheck(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<DatabaseStartupCheck>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Start_OtherException_WrapsAsInvalidOperation()
    {
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => throw new Exception("boom"));
        var sp = services.BuildServiceProvider();

        var check = new DatabaseStartupCheck(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<DatabaseStartupCheck>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));
    }
}
