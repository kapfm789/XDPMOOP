using FluentValidation;
using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.Inventory.CreateTransfer;

public sealed record CreateTransferCommand(
    Guid FromBranchId, Guid ToBranchId, IReadOnlyList<TransferLine> Items, Guid? CreatedBy, bool IncludeCost);

public sealed class CreateTransferValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferValidator()
    {
        RuleFor(command => command.FromBranchId).NotEmpty().WithMessage("Chọn chi nhánh gửi");
        RuleFor(command => command.ToBranchId).NotEmpty().WithMessage("Chọn chi nhánh nhận");
        // UC-INV-03 AC-7.
        RuleFor(command => command.ToBranchId)
            .NotEqual(command => command.FromBranchId).WithMessage("Chi nhánh nhận phải khác chi nhánh gửi");
        RuleFor(command => command.Items).NotEmpty().WithMessage("Phiếu cần ít nhất một dòng");
        RuleForEach(command => command.Items).ChildRules(line =>
        {
            line.RuleFor(item => item.SkuId).NotEmpty().WithMessage("Chọn SKU");
            line.RuleFor(item => item.Quantity).GreaterThan(0).WithMessage("Số lượng phải dương");
        });
    }
}

// UC-INV-03: lưu phiếu chuyển kho ở Draft; tồn chưa bị tác động tới khi xuất.
public sealed class CreateTransferHandler(
    IUnitOfWork unitOfWork, IStockTransferRepository transfers, IReferenceRepository references)
{
    private static readonly CreateTransferValidator Validator = new();

    public async Task<TransferDto> Handle(CreateTransferCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        foreach (var branchId in new[] { command.FromBranchId, command.ToBranchId })
        {
            var branch = await references.FindBranchAsync(branchId, ct)
                ?? throw new ReferenceNotReadyException("Chi nhánh", branchId);
            branch.EnsureActive();
        }

        var known = (await references.ListSkusAsync(command.Items.Select(item => item.SkuId).ToHashSet(), ct))
            .Select(sku => sku.SkuId).ToHashSet();
        if (command.Items.FirstOrDefault(item => !known.Contains(item.SkuId)) is { } unknown)
            throw new ReferenceNotReadyException("SKU", unknown.SkuId);

        var transfer = StockTransfer.Create(command.FromBranchId, command.ToBranchId, command.Items, command.CreatedBy);
        transfers.Add(transfer);

        await transaction.CommitAsync(ct);
        return await references.ToDtoAsync(transfer, command.IncludeCost, ct);
    }
}
