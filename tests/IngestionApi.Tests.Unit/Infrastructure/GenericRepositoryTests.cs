using IngestionApi.Domain.Entities;
using IngestionApi.Infrastructure.Persistence.Repositories;
using IngestionApi.Tests.Unit.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Tests.Unit.Infrastructure;

public class GenericRepositoryTests
{
    [Fact]
    public async Task Add_PersistsAfterSave()
    {
        await using var ctx = InMemoryDbContextFactory.Create();
        var repo = new GenericRepository<Transaction, Guid>(ctx);

        var t = new Transaction { OperationId = "op", VendorId = "v", BagId = "b", Currency = "USD", Value = 1m, TransactionType = TransactionType.Declared };
        // Set Id via reflection since setter is protected
        typeof(Transaction).BaseType!.GetProperty("Id")!.SetValue(t, Guid.NewGuid());
        repo.Add(t);
        await ctx.SaveChangesAsync();

        Assert.Equal(1, await ctx.Set<Transaction>().CountAsync());
    }

    [Fact]
    public async Task AddRange_PersistsAll()
    {
        await using var ctx = InMemoryDbContextFactory.Create();
        var repo = new GenericRepository<Transaction, Guid>(ctx);

        var list = Enumerable.Range(0, 3).Select(_ =>
        {
            var t = new Transaction { OperationId = "op", VendorId = "v", BagId = "b", Currency = "USD", Value = 1m, TransactionType = TransactionType.Declared };
            typeof(Transaction).BaseType!.GetProperty("Id")!.SetValue(t, Guid.NewGuid());
            return t;
        }).ToList();
        repo.AddRange(list);
        await ctx.SaveChangesAsync();

        Assert.Equal(3, await ctx.Set<Transaction>().CountAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEntity()
    {
        await using var ctx = InMemoryDbContextFactory.Create();
        var repo = new GenericRepository<Transaction, Guid>(ctx);
        var id = Guid.NewGuid();
        var t = new Transaction { OperationId = "op", VendorId = "v", BagId = "b", Currency = "USD", Value = 1m, TransactionType = TransactionType.Declared };
        typeof(Transaction).BaseType!.GetProperty("Id")!.SetValue(t, id);
        repo.Add(t);
        await ctx.SaveChangesAsync();

        var found = await repo.GetByIdAsync(id);
        Assert.NotNull(found);
        Assert.Equal(id, found!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        await using var ctx = InMemoryDbContextFactory.Create();
        var repo = new GenericRepository<Transaction, Guid>(ctx);
        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Remove_DeletesEntity()
    {
        await using var ctx = InMemoryDbContextFactory.Create();
        var repo = new GenericRepository<Transaction, Guid>(ctx);
        var id = Guid.NewGuid();
        var t = new Transaction { OperationId = "op", VendorId = "v", BagId = "b", Currency = "USD", Value = 1m, TransactionType = TransactionType.Declared };
        typeof(Transaction).BaseType!.GetProperty("Id")!.SetValue(t, id);
        repo.Add(t);
        await ctx.SaveChangesAsync();

        repo.Remove(t);
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await ctx.Set<Transaction>().CountAsync());
    }
}
