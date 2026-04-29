using IngestionApi.Application.Common;
using IngestionApi.Domain.Entities;
using IngestionApi.Infrastructure.Persistence;
using IngestionApi.Tests.Unit.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Tests.Unit.Infrastructure;

public class UnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_HappyPath_ReturnsAffectedCount()
    {
        await using var ctx = InMemoryDbContextFactory.Create();
        var t = new Transaction { OperationId = "op", VendorId = "v", BagId = "b", Currency = "USD", Value = 1m, TransactionType = TransactionType.Declared };
        typeof(Transaction).BaseType!.GetProperty("Id")!.SetValue(t, Guid.NewGuid());
        ctx.Add(t);

        var uow = new UnitOfWork(ctx);
        var affected = await uow.SaveChangesAsync();

        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task SaveChangesAsync_OtherDbUpdateError_Rethrows()
    {
        await using var ctx = new ThrowingDbContext(new InvalidOperationException("other"));
        var uow = new UnitOfWork(ctx);
        await Assert.ThrowsAsync<DbUpdateException>(() => uow.SaveChangesAsync());
    }

    private sealed class ThrowingDbContext : AppDbContext
    {
        private readonly Exception _baseException;

        public ThrowingDbContext(Exception baseException)
            : base(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
        {
            _baseException = baseException;
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            throw new DbUpdateException("save failed", _baseException);
    }
}

public class InfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersServices()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new Microsoft.Extensions.Configuration.ConfigurationManager();
        config["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True";

        IngestionApi.Infrastructure.DependencyInjection.AddInfrastructure(services, config);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IngestionApi.Domain.Common.IUnitOfWork));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IngestionApi.Domain.Common.IRepository<Transaction, Guid>));
    }
}
