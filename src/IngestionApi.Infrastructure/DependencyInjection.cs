using IngestionApi.Domain.Common;
using IngestionApi.Domain.Entities;
using IngestionApi.Infrastructure.Persistence;
using IngestionApi.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IngestionApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContextPool<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql
                    .EnableRetryOnFailure(3)
                    .CommandTimeout(30)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register repositories
        services.AddScoped<IRepository<Transaction, Guid>, GenericRepository<Transaction, Guid>>();

        return services;
    }
}
