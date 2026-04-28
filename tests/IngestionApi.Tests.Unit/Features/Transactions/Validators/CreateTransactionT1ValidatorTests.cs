using FluentValidation.TestHelper;
using IngestionApi.Application.Features.Transactions.Commands;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Application.Features.Transactions.Validators;
using Xunit;

namespace IngestionApi.Tests.Unit.Features.Transactions.Validators;

public class CreateTransactionT1ValidatorTests
{
    private readonly CreateTransactionT1Validator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var command = new CreateTransactionT1Command("OP123", "BAG123",
            new List<BagDto> { new(100m, 50m, 10m, "USD") }, "V123");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void WithZeroDifferenceValue_ShouldPass()
    {
        var command = new CreateTransactionT1Command("OP123", "BAG123",
            new List<BagDto> { new(100m, 50m, 0m, "USD") }, "V123");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ProcessedValueNegative_ShouldFail()
    {
        var command = new CreateTransactionT1Command("OP123", "BAG123",
            new List<BagDto> { new(100m, -10m, 10m, "USD") }, "V123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Bags[0].ProcessedValue");
    }

    [Fact]
    public void DifferenceValueNegative_ShouldFail()
    {
        var command = new CreateTransactionT1Command("OP123", "BAG123",
            new List<BagDto> { new(100m, 50m, -5m, "USD") }, "V123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Bags[0].DifferenceValue");
    }
}
