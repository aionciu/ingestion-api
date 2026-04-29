using IngestionApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Tests.Unit.TestHelpers;

internal static class InMemoryDbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
