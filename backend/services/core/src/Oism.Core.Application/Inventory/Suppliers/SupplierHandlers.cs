using FluentValidation;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory.Suppliers;

public sealed record CreateSupplierCommand(string Name, string? Phone);

public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator()
    {
        RuleFor(command => command.Name).NotEmpty().WithMessage("Nhập tên nhà cung cấp").MaximumLength(200);
        RuleFor(command => command.Phone).MaximumLength(20);
    }
}

public sealed class CreateSupplierHandler(IUnitOfWork unitOfWork, IPurchaseReceiptRepository receipts)
{
    private static readonly CreateSupplierValidator Validator = new();

    public async Task<SupplierDto> Handle(CreateSupplierCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);
        var supplier = Supplier.Create(command.Name, command.Phone);
        receipts.Add(supplier);
        await transaction.CommitAsync(ct);
        return SupplierDto.From(supplier);
    }
}

public sealed class ListSuppliersHandler(IStockQueries queries)
{
    public Task<IReadOnlyList<SupplierDto>> Handle(CancellationToken ct) => queries.ListSuppliersAsync(ct);
}
