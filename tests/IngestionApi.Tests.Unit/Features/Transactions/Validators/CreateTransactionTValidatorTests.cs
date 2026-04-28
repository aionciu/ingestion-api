using FluentValidation.TestHelper;
using IngestionApi.Application.Features.Transactions.Commands;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Application.Features.Transactions.Validators;
using Xunit;

namespace IngestionApi.Tests.Unit.Features.Transactions.Validators;

public class CreateTransactionTValidatorTests
{
    private readonly CreateTransactionTValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var command = new CreateTransactionTCommand(
            OperationId: "OP123",
            BagId: "BAG123",
            Bags: new List<BagDto> { new(DeclaredValue: 100m, ProcessedValue: 0m, DifferenceValue: 0m, Currency: "USD") },
            VendorId: "V123");

        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeclaredValueZero_ShouldFail()
    {
        var command = new CreateTransactionTCommand("OP123", "BAG123",
            new List<BagDto> { new(0m, 0m, 0m, "USD") }, "V123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Bags);
    }

    [Fact]
    public void ProcessedValueNonZero_ShouldFail()
    {
        var command = new CreateTransactionTCommand("OP123", "BAG123",
            new List<BagDto> { new(100m, 50m, 0m, "USD") }, "V123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Bags);
    }

    [Fact]
    public void DifferenceValueNonZero_ShouldFail()
    {
        var command = new CreateTransactionTCommand("OP123", "BAG123",
            new List<BagDto> { new(100m, 0m, 10m, "USD") }, "V123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Bags);
    }

    [Fact]
    public void EmptyBags_ShouldFail()
    {
        var command = new CreateTransactionTCommand("OP123", "BAG123", new List<BagDto>(), "V123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Bags);
    }
}
