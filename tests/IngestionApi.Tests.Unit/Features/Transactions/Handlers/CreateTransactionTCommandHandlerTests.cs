using FluentValidation;
using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.Commands;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Domain.Common;
using IngestionApi.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IngestionApi.Tests.Unit.Features.Transactions.Handlers;

public class CreateTransactionTCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<IRepository<Transaction, Guid>> _mockRepository = new();
    private readonly Mock<ILogger<CreateTransactionTCommandHandler>> _mockLogger = new();
    private readonly Mock<IValidator<CreateTransactionTCommand>> _mockValidator = new();
    private readonly CreateTransactionTCommandHandler _handler;

    public CreateTransactionTCommandHandlerTests()
    {
        _handler = new CreateTransactionTCommandHandler(_mockLogger.Object, _mockUnitOfWork.Object, _mockRepository.Object, _mockValidator.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ReturnsSuccess()
    {
        var command = new CreateTransactionTCommand(
            "OP123", "BAG123",
            new List<BagDto> { new(100m, 0m, 0m, "USD") },
            "V123");

        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ConflictException_ReturnsConflict()
    {
        var command = new CreateTransactionTCommand(
            "OP123", "BAG123",
            new List<BagDto> { new(100m, 0m, 0m, "USD") },
            "V123");

        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Duplicate constraint"));

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsConflict);
    }

    [Fact]
    public async Task HandleAsync_MultipleBags_CreatesOneTransactionPerBag()
    {
        var command = new CreateTransactionTCommand(
            "OP123", "BAG123",
            new List<BagDto>
            {
                new(100m, 0m, 0m, "USD"),
                new(200m, 0m, 0m, "EUR")
            },
            "V123");

        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _mockRepository.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Exactly(2));
    }
}
