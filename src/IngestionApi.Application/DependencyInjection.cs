using FluentValidation;
using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace IngestionApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Register command handlers
        services.AddScoped<ICommandHandler<CreateTransactionTCommand>, CreateTransactionTCommandHandler>();
        services.AddScoped<ICommandHandler<CreateTransactionT1Command>, CreateTransactionT1CommandHandler>();

        return services;
    }
}
