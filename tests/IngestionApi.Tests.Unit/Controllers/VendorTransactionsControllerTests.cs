using FluentValidation;
using FluentValidation.Results;
using IngestionApi.Api.Contracts;
using IngestionApi.Api.Controllers;
using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IngestionApi.Tests.Unit.Controllers;

public class VendorTransactionsControllerTests
{
    private readonly Mock<ICommandHandler<CreateTransactionTCommand>> _tHandler = new();
    private readonly Mock<ICommandHandler<CreateTransactionT1Command>> _t1Handler = new();
    private readonly Mock<IValidator<CreateTransactionTCommand>> _tValidator = new();
    private readonly Mock<IValidator<CreateTransactionT1Command>> _t1Validator = new();

    private VendorTransactionsController CreateController(string? vendorId = "V1")
    {
        var c = new VendorTransactionsController(
            _tHandler.Object, _t1Handler.Object,
            _tValidator.Object, _t1Validator.Object,
            NullLogger<VendorTransactionsController>.Instance);
        var http = new DefaultHttpContext();
        if (vendorId is not null) http.Request.Headers["X-Vendor-Id"] = vendorId;
        c.ControllerContext = new ControllerContext { HttpContext = http };
        return c;
    }

    private static CreateTransactionRequest Request() => new(
        "OP", "BAG", new List<BagItemDto> { new(100m, 0m, 0m, "USD") });

    [Fact]
    public async Task T_MissingVendorHeader_ReturnsBadRequest()
    {
        var c = CreateController(vendorId: null);
        var result = await c.CreateTransactionT(Request(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task T_ValidationFails_ReturnsBadRequest()
    {
        _tValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionTCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("OperationId", "required") }));
        var c = CreateController();
        var result = await c.CreateTransactionT(Request(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task T_HandlerSuccess_ReturnsAccepted()
    {
        _tValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionTCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _tHandler.Setup(h => h.HandleAsync(It.IsAny<CreateTransactionTCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var c = CreateController();
        var result = await c.CreateTransactionT(Request(), CancellationToken.None);
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task T_HandlerConflict_ReturnsConflict()
    {
        _tValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionTCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _tHandler.Setup(h => h.HandleAsync(It.IsAny<CreateTransactionTCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict());
        var c = CreateController();
        var result = await c.CreateTransactionT(Request(), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task T_HandlerFailure_ReturnsBadRequest()
    {
        _tValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionTCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _tHandler.Setup(h => h.HandleAsync(It.IsAny<CreateTransactionTCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("nope"));
        var c = CreateController();
        var result = await c.CreateTransactionT(Request(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task T1_MissingVendorHeader_ReturnsBadRequest()
    {
        var c = CreateController(vendorId: null);
        var result = await c.CreateTransactionT1(Request(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task T1_ValidationFails_ReturnsBadRequest()
    {
        _t1Validator.Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionT1Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("OperationId", "required") }));
        var c = CreateController();
        var result = await c.CreateTransactionT1(Request(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task T1_HandlerSuccess_ReturnsAccepted()
    {
        _t1Validator.Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionT1Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _t1Handler.Setup(h => h.HandleAsync(It.IsAny<CreateTransactionT1Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var c = CreateController();
        var result = await c.CreateTransactionT1(Request(), CancellationToken.None);
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task T1_HandlerConflict_ReturnsConflict()
    {
        _t1Validator.Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionT1Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _t1Handler.Setup(h => h.HandleAsync(It.IsAny<CreateTransactionT1Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict());
        var c = CreateController();
        var result = await c.CreateTransactionT1(Request(), CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task T1_HandlerFailure_ReturnsBadRequest()
    {
        _t1Validator.Setup(v => v.ValidateAsync(It.IsAny<CreateTransactionT1Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _t1Handler.Setup(h => h.HandleAsync(It.IsAny<CreateTransactionT1Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("nope"));
        var c = CreateController();
        var result = await c.CreateTransactionT1(Request(), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
