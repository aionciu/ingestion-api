using IngestionApi.Application.Common;
using IngestionApi.Domain.Common;
using Microsoft.Extensions.Logging;

namespace IngestionApi.Application.Features.Transactions.Commands;

public record CreateTransactionCommand(string VendorId, string TransactionId, string Payload);

public class CreateTransactionCommandHandler : ICommandHandler<CreateTransactionCommand>
{
    private readonly ILogger<CreateTransactionCommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTransactionCommandHandler(
        ILogger<CreateTransactionCommandHandler> logger,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Transaction received from vendor {VendorId} with ID {TransactionId}",
            command.VendorId, command.TransactionId);

        // Placeholder: validation and processing would go here

        _logger.LogInformation("Transaction {TransactionId} processed successfully", command.TransactionId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
