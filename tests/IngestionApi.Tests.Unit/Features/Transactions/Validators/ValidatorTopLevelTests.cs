using FluentValidation.TestHelper;
using IngestionApi.Application.Features.Transactions.Commands;
using IngestionApi.Application.Features.Transactions.DTOs;
using IngestionApi.Application.Features.Transactions.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace IngestionApi.Tests.Unit.Features.Transactions.Validators;

public class ValidatorTopLevelTests
{
    private static List<BagDto> ValidBagsT() => new() { new(100m, 0m, 0m, "USD") };
    private static List<BagDto> ValidBagsT1() => new() { new(100m, 50m, 10m, "USD") };

    [Fact]
    public void T_EmptyOperationId_Fails()
    {
        var v = new CreateTransactionTValidator();
        var r = v.TestValidate(new CreateTransactionTCommand("", "BAG", ValidBagsT(), "V"));
        r.ShouldHaveValidationErrorFor(c => c.OperationId);
    }

    [Fact]
    public void T_EmptyBagId_Fails()
    {
        var v = new CreateTransactionTValidator();
        var r = v.TestValidate(new CreateTransactionTCommand("OP", "", ValidBagsT(), "V"));
        r.ShouldHaveValidationErrorFor(c => c.BagId);
    }

    [Fact]
    public void T_EmptyVendorId_Fails()
    {
        var v = new CreateTransactionTValidator();
        var r = v.TestValidate(new CreateTransactionTCommand("OP", "B", ValidBagsT(), ""));
        r.ShouldHaveValidationErrorFor(c => c.VendorId);
    }

    [Fact]
    public void T_EmptyCurrency_Fails()
    {
        var v = new CreateTransactionTValidator();
        var r = v.TestValidate(new CreateTransactionTCommand("OP", "B",
            new List<BagDto> { new(100m, 0m, 0m, "") }, "V"));
        r.ShouldHaveValidationErrorFor("Bags[0].Currency");
    }

    [Fact]
    public void T1_EmptyOperationId_Fails()
    {
        var v = new CreateTransactionT1Validator();
        var r = v.TestValidate(new CreateTransactionT1Command("", "B", ValidBagsT1(), "V"));
        r.ShouldHaveValidationErrorFor(c => c.OperationId);
    }

    [Fact]
    public void T1_EmptyBagId_Fails()
    {
        var v = new CreateTransactionT1Validator();
        var r = v.TestValidate(new CreateTransactionT1Command("OP", "", ValidBagsT1(), "V"));
        r.ShouldHaveValidationErrorFor(c => c.BagId);
    }

    [Fact]
    public void T1_EmptyVendorId_Fails()
    {
        var v = new CreateTransactionT1Validator();
        var r = v.TestValidate(new CreateTransactionT1Command("OP", "B", ValidBagsT1(), ""));
        r.ShouldHaveValidationErrorFor(c => c.VendorId);
    }

    [Fact]
    public void T1_EmptyBags_Fails()
    {
        var v = new CreateTransactionT1Validator();
        var r = v.TestValidate(new CreateTransactionT1Command("OP", "B", new List<BagDto>(), "V"));
        r.ShouldHaveValidationErrorFor(c => c.Bags);
    }

    [Fact]
    public void T1_EmptyCurrency_Fails()
    {
        var v = new CreateTransactionT1Validator();
        var r = v.TestValidate(new CreateTransactionT1Command("OP", "B",
            new List<BagDto> { new(100m, 50m, 10m, "") }, "V"));
        r.ShouldHaveValidationErrorFor("Bags[0].Currency");
    }

    [Fact]
    public void T1_DeclaredZero_Fails()
    {
        var v = new CreateTransactionT1Validator();
        var r = v.TestValidate(new CreateTransactionT1Command("OP", "B",
            new List<BagDto> { new(0m, 50m, 10m, "USD") }, "V"));
        r.ShouldHaveValidationErrorFor("Bags[0].DeclaredValue");
    }
}

public class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersHandlersAndValidators()
    {
        var services = new ServiceCollection();
        IngestionApi.Application.DependencyInjection.AddApplication(services);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IngestionApi.Application.Common.ICommandHandler<CreateTransactionTCommand>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IngestionApi.Application.Common.ICommandHandler<CreateTransactionT1Command>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(FluentValidation.IValidator<CreateTransactionTCommand>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(FluentValidation.IValidator<CreateTransactionT1Command>));
    }
}
