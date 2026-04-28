using IngestionApi.Application.Common;
using IngestionApi.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IngestionApi.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    private const int SqlDuplicateKeyRowError = 2601;       // duplicate key row in unique index
    private const int SqlUniqueConstraintViolation = 2627;  // unique constraint violation

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            if (ex.GetBaseException() is SqlException sqlEx &&
                (sqlEx.Number == SqlDuplicateKeyRowError || sqlEx.Number == SqlUniqueConstraintViolation))
            {
                throw new ConflictException("A transaction with these details already exists.", ex);
            }

            throw;
        }
    }
}
