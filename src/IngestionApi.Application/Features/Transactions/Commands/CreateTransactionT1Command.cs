using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Domain.Common;
using IngestionApi.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Application.Features.Transactions.Commands;

public record CreateTransactionT1Command(
    string OperationId,
    string BagId,
    List<BagDto> Bags,
    string VendorId);

public class CreateTransactionT1CommandHandler : ICommandHandler<CreateTransactionT1Command>
{
    private readonly ILogger<CreateTransactionT1CommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Transaction, Guid> _repository;

    public CreateTransactionT1CommandHandler(
        ILogger<CreateTransactionT1CommandHandler> logger,
        IUnitOfWork unitOfWork,
        IRepository<Transaction, Guid> repository)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _repository = repository;
    }

    public async Task<Result> HandleAsync(CreateTransactionT1Command command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("T+1 transaction received from vendor {VendorId} with operation {OperationId}",
            command.VendorId, command.OperationId);

        try
        {
            // For T+1 endpoint: create 3 transactions per bag (one for each value type)
            var transactions = command.Bags.SelectMany(bag => new[]
            {
                new Transaction
                {
                    OperationId = command.OperationId,
                    VendorId = command.VendorId,
                    BagId = command.BagId,
                    Currency = bag.Currency,
                    Value = bag.DeclaredValue,
                    TransactionType = 1
                },
                new Transaction
                {
                    OperationId = command.OperationId,
                    VendorId = command.VendorId,
                    BagId = command.BagId,
                    Currency = bag.Currency,
                    Value = bag.ProcessedValue,
                    TransactionType = 2
                },
                new Transaction
                {
                    OperationId = command.OperationId,
                    VendorId = command.VendorId,
                    BagId = command.BagId,
                    Currency = bag.Currency,
                    Value = bag.DifferenceValue,
                    TransactionType = 3
                }
            }).ToList();
            _repository.AddRange(transactions);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("T+1 transaction {OperationId} processed successfully with {TransactionCount} rows",
                command.OperationId, transactions.Count);
            return Result.Success();
        }
        catch (ConflictException)
        {
            _logger.LogWarning("T+1 transaction {OperationId} conflicts with existing data", command.OperationId);
            return Result.Conflict();
        }
    }
}
