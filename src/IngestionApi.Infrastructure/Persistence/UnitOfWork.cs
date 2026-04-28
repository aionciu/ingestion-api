using IngestionApi.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true ||
            ex.InnerException?.Message.Contains("duplicate key") == true)
        {
            // Re-throw as a more specific exception that the Application layer can handle
            throw new InvalidOperationException("A duplicate constraint violation occurred.", ex);
        }
    }
}
