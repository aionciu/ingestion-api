using IngestionApi.Application.Common;
using IngestionApi.Application.Features.Transactions.Commands;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Domain.Common;
using IngestionApi.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IngestionApi.Tests.Unit.Features.Transactions.Handlers;

public class CreateTransactionT1CommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<IRepository<Transaction, Guid>> _mockRepository = new();
    private readonly Mock<ILogger<CreateTransactionT1CommandHandler>> _mockLogger = new();
    private readonly CreateTransactionT1CommandHandler _handler;

    public CreateTransactionT1CommandHandlerTests()
    {
        _handler = new CreateTransactionT1CommandHandler(_mockLogger.Object, _mockUnitOfWork.Object, _mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CreatesThreeTransactionsPerBag()
    {
        var command = new CreateTransactionT1Command(
            "OP123", "BAG123",
            new List<BagDto> { new(100m, 50m, 10m, "USD") },
            "V123");

        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Should create 3 transactions for 1 bag
        _mockRepository.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_ConflictException_ReturnsConflict()
    {
        var command = new CreateTransactionT1Command(
            "OP123", "BAG123",
            new List<BagDto> { new(100m, 50m, 10m, "USD") },
            "V123");

        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Duplicate constraint"));

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsConflict);
    }

    [Fact]
    public async Task HandleAsync_MultipleBags_CreatesNineTransactions()
    {
        var command = new CreateTransactionT1Command(
            "OP123", "BAG123",
            new List<BagDto>
            {
                new(100m, 50m, 10m, "USD"),
                new(200m, 100m, 20m, "EUR"),
                new(300m, 150m, 30m, "GBP")
            },
            "V123");

        _mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(9);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Should create 3 transactions for each of 3 bags = 9 total
        _mockRepository.Verify(x => x.Add(It.IsAny<Transaction>()), Times.Exactly(9));
    }
}
