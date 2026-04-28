using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.Commands;
using IngestionApi.Domain.Common;
using IngestionApi.Infrastructure.Persistence;
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
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register command handlers
        services.AddScoped<ICommandHandler<CreateTransactionTCommand>, CreateTransactionTCommandHandler>();
        services.AddScoped<ICommandHandler<CreateTransactionT1Command>, CreateTransactionT1CommandHandler>();

        return services;
    }
}
