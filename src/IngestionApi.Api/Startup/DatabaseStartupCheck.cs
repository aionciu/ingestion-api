using IngestionApi.Infrastructure.Persistence;

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
        _logger.LogInformation("Checking database connectivity...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                _logger.LogError("Database connectivity check failed: server unreachable.");
                throw new InvalidOperationException("Cannot connect to the database.");
            }

            _logger.LogInformation("Database connectivity check passed.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connectivity check failed.");
            throw new InvalidOperationException("Cannot connect to the database.", ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
