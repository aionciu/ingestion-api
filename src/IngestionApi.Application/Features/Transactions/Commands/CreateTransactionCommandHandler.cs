using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Domain.Common;
using IngestionApi.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Application.Features.Transactions.Commands;

public record CreateTransactionTCommand(
    string OperationId,
    string BagId,
    List<BagDto> Bags,
    string VendorId);

public class CreateTransactionTCommandHandler : ICommandHandler<CreateTransactionTCommand>
{
    private readonly ILogger<CreateTransactionTCommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Transaction, Guid> _repository;

    public CreateTransactionTCommandHandler(
        ILogger<CreateTransactionTCommandHandler> logger,
        IUnitOfWork unitOfWork,
        IRepository<Transaction, Guid> repository)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _repository = repository;
    }

    public async Task<Result> HandleAsync(CreateTransactionTCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("T transaction received from vendor {VendorId} with operation {OperationId}",
            command.VendorId, command.OperationId);

        try
        {
            // For T endpoint: only create DeclaredValue transactions
            // Validator ensures ProcessedValue=0 and DifferenceValue=0
            var transactions = command.Bags.Select(bag => new Transaction
            {
                OperationId = command.OperationId,
                VendorId = command.VendorId,
                BagId = command.BagId,
                Currency = bag.Currency,
                Value = bag.DeclaredValue,
                TransactionType = TransactionType.Declared
            }).ToList();
            _repository.AddRange(transactions);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("T transaction {OperationId} processed successfully", command.OperationId);
            return Result.Success();
        }
        catch (ConflictException)
        {
            _logger.LogWarning("T transaction {OperationId} conflicts with existing data", command.OperationId);
            return Result.Conflict();
        }
    }
}
