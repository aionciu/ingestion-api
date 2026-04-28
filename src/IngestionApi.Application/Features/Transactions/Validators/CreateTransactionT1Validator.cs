using FluentValidation;
using IngestionApi.Application.Features.Transactions.Commands;

namespace IngestionApi.Application.Features.Transactions.Validators;

public class CreateTransactionT1Validator : AbstractValidator<CreateTransactionT1Command>
{
    public CreateTransactionT1Validator()
    {
        RuleFor(x => x.OperationId)
            .NotEmpty().WithMessage("OperationId is required");

        RuleFor(x => x.BagId)
            .NotEmpty().WithMessage("BagId is required");

        RuleFor(x => x.VendorId)
            .NotEmpty().WithMessage("VendorId is required");

        RuleFor(x => x.Bags)
            .NotEmpty().WithMessage("At least one bag is required");

        RuleForEach(x => x.Bags)
            .ChildRules(bag =>
            {
                bag.RuleFor(b => b.Currency)
                    .NotEmpty().WithMessage("Currency is required");

                bag.RuleFor(b => b.DeclaredValue)
                    .GreaterThan(0).WithMessage("DeclaredValue must be greater than 0");

                bag.RuleFor(b => b.ProcessedValue)
                    .GreaterThanOrEqualTo(0).WithMessage("ProcessedValue must be >= 0");

                bag.RuleFor(b => b.DifferenceValue)
                    .GreaterThanOrEqualTo(0).WithMessage("DifferenceValue must be >= 0");
            });
    }
}
