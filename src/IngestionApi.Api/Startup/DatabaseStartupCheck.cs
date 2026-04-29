using IngestionApi.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IngestionApi.Api.Startup;

public class DatabaseStartupCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseStartupCheck> _logger;

    public DatabaseStartupCheck(IServiceScopeFactory scopeFactory, ILogger<DatabaseStartupCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
            throw new InvalidOperationException("Cannot connect to the database.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
