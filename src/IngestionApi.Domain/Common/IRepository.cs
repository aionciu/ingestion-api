namespace IngestionApi.Domain.Common;

public interface IRepository<TEntity, TId>
    where TEntity : Entity<TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default);
    void Add(TEntity entity);
    void Remove(TEntity entity);
}
