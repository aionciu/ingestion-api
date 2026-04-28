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

        // Transaction creation and persistence placeholder
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("T transaction {OperationId} processed successfully", command.OperationId);
        return Result.Success();
    }
}
