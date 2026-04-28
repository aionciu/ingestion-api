using IngestionApi.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Infrastructure.Persistence.Repositories;

public class GenericRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public GenericRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        DbSet.FindAsync(new object?[] { id }, ct).AsTask();

    public async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        await DbSet.ToListAsync(ct);

    public void Add(TEntity entity) => DbSet.Add(entity);

    public void Remove(TEntity entity) => DbSet.Remove(entity);
}
