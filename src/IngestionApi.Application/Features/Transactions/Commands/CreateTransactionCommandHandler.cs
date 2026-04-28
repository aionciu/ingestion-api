using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Domain.Common;
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

    public CreateTransactionTCommandHandler(
        ILogger<CreateTransactionTCommandHandler> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(CreateTransactionTCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("T transaction received from vendor {VendorId} with operation {OperationId}",
            command.VendorId, command.OperationId);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("T transaction {OperationId} processed successfully", command.OperationId);
            return Result.Success();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("duplicate constraint violation"))
        {
            _logger.LogWarning("T transaction {OperationId} conflicts with existing data", command.OperationId);
            return Result.Failure("A transaction with the same unique constraint already exists.");
        }
    }
}
