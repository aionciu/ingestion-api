using FluentValidation;
using IngestionApi.Api.Contracts;
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
    private readonly IValidator<CreateTransactionTCommand> _createTransactionTValidator;
    private readonly IValidator<CreateTransactionT1Command> _createTransactionT1Validator;
    private readonly ILogger<VendorTransactionsController> _logger;

    public VendorTransactionsController(
        ICommandHandler<CreateTransactionTCommand> createTransactionTHandler,
        ICommandHandler<CreateTransactionT1Command> createTransactionT1Handler,
        IValidator<CreateTransactionTCommand> createTransactionTValidator,
        IValidator<CreateTransactionT1Command> createTransactionT1Validator,
        ILogger<VendorTransactionsController> logger)
    {
        _createTransactionTHandler = createTransactionTHandler;
        _createTransactionT1Handler = createTransactionT1Handler;
        _createTransactionTValidator = createTransactionTValidator;
        _createTransactionT1Validator = createTransactionT1Validator;
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

        var validationResult = await _createTransactionTValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("T transaction validation failed for {OperationId}", command.OperationId);
            return BadRequest(new { errors = validationResult.Errors.Select(e => new { property = e.PropertyName, message = e.ErrorMessage }) });
        }

        var result = await _createTransactionTHandler.HandleAsync(command, cancellationToken);

        return result switch
        {
            { IsSuccess: true } => Accepted(),
            { IsConflict: true } => Conflict(new { detail = "A transaction with these bag details already exists." }),
            { Error: not null } => BadRequest(new { detail = result.Error }),
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

        var validationResult = await _createTransactionT1Validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("T+1 transaction validation failed for {OperationId}", command.OperationId);
            return BadRequest(new { errors = validationResult.Errors.Select(e => new { property = e.PropertyName, message = e.ErrorMessage }) });
        }

        var result = await _createTransactionT1Handler.HandleAsync(command, cancellationToken);

        return result switch
        {
            { IsSuccess: true } => Accepted(),
            { IsConflict: true } => Conflict(new { detail = "A transaction with these bag details already exists." }),
            { Error: not null } => BadRequest(new { detail = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
