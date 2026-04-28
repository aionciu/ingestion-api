using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.Commands;
using IngestionApi.Application.Features.Transactions.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IngestionApi.Api.Controllers;

[ApiController]
[Route("api/vendor-transactions")]
public class VendorTransactionsController : ControllerBase
{
    private readonly ICommandHandler<CreateTransactionTCommand> _createTransactionTHandler;
    private readonly ICommandHandler<CreateTransactionT1Command> _createTransactionT1Handler;
    private readonly ILogger<VendorTransactionsController> _logger;

    public VendorTransactionsController(
        ICommandHandler<CreateTransactionTCommand> createTransactionTHandler,
        ICommandHandler<CreateTransactionT1Command> createTransactionT1Handler,
        ILogger<VendorTransactionsController> logger)
    {
        _createTransactionTHandler = createTransactionTHandler;
        _createTransactionT1Handler = createTransactionT1Handler;
        _logger = logger;
    }

    /// <summary>Create transaction for T (today)</summary>
    [HttpPost("T")]
    public async Task<IActionResult> CreateTransactionT(
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var vendorId = Request.Headers["X-Vendor-Id"].ToString();

        if (string.IsNullOrWhiteSpace(vendorId))
        {
            _logger.LogWarning("T transaction request missing X-Vendor-Id header");
            return BadRequest(new { detail = "X-Vendor-Id header is required" });
        }

        var command = new CreateTransactionTCommand(
            request.OperationId,
            request.BagId,
            request.Bags.ConvertAll(b => new BagDto(b.DeclaredValue, b.ProcessedValue, b.DifferenceValue, b.Currency)),
            vendorId);

        var result = await _createTransactionTHandler.HandleAsync(command, cancellationToken);

        return result switch
        {
            { IsSuccess: true } => Accepted(),
            { IsConflict: true } => Conflict(new { detail = "A transaction with these bag details already exists." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>Create transaction for T+1 (tomorrow)</summary>
    [HttpPost("T+1")]
    public async Task<IActionResult> CreateTransactionT1(
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var vendorId = Request.Headers["X-Vendor-Id"].ToString();

        if (string.IsNullOrWhiteSpace(vendorId))
        {
            _logger.LogWarning("T+1 transaction request missing X-Vendor-Id header");
            return BadRequest(new { detail = "X-Vendor-Id header is required" });
        }

        var command = new CreateTransactionT1Command(
            request.OperationId,
            request.BagId,
            request.Bags.ConvertAll(b => new BagDto(b.DeclaredValue, b.ProcessedValue, b.DifferenceValue, b.Currency)),
            vendorId);

        var result = await _createTransactionT1Handler.HandleAsync(command, cancellationToken);

        return result switch
        {
            { IsSuccess: true } => Accepted(),
            { IsConflict: true } => Conflict(new { detail = "A transaction with these bag details already exists." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}

public record CreateTransactionRequest(
    string OperationId,
    string BagId,
    List<BagItemDto> Bags);

public record BagItemDto(
    decimal DeclaredValue,
    decimal ProcessedValue,
    decimal DifferenceValue,
    string Currency);
