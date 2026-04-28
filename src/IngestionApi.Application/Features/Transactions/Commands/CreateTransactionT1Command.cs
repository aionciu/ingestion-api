using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Domain.Common;
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

    public CreateTransactionT1CommandHandler(
        ILogger<CreateTransactionT1CommandHandler> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(CreateTransactionT1Command command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("T+1 transaction received from vendor {VendorId} with operation {OperationId}",
            command.VendorId, command.OperationId);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("T+1 transaction {OperationId} processed successfully", command.OperationId);
            return Result.Success();
        }
        catch (ConflictException)
        {
            _logger.LogWarning("T+1 transaction {OperationId} conflicts with existing data", command.OperationId);
            return Result.Conflict();
        }
    }
}
