using IngestionApi.Application.Common;
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
        catch (DbUpdateException ex)
        {
            // Check if this is a constraint violation by inspecting the entries
            var conflictEntries = ex.Entries
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .ToList();

            // If we have entries and a database exception, it's likely a constraint/validation error
            // Re-throw as ConflictException if the error message suggests a unique constraint
            if (conflictEntries.Any() &&
                (ex.InnerException?.Message.Contains("UNIQUE") == true ||
                 ex.InnerException?.Message.Contains("duplicate") == true ||
                 ex.InnerException?.Message.Contains("constraint") == true))
            {
                throw new ConflictException("A transaction with these details already exists.", ex);
            }

            // For other database errors, re-throw the original exception
            throw;
        }
    }
}
