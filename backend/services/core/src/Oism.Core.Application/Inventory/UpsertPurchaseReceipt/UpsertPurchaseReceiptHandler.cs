using FluentValidation;
using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.References;
using Oism.SharedKernel;

namespace Oism.Core.Application.Inventory.UpsertPurchaseReceipt;

// Id null: tạo phiếu Draft tại BranchId. Id có giá trị: sửa phiếu còn Draft; chi nhánh của phiếu không đổi.
public sealed record UpsertPurchaseReceiptCommand(
    Guid? Id, Guid BranchId, Guid SupplierId, string? Note, IReadOnlyList<PurchaseLine> Items);

public sealed class UpsertPurchaseReceiptValidator : AbstractValidator<UpsertPurchaseReceiptCommand>
{
    public UpsertPurchaseReceiptValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty().When(command => command.Id is null).WithMessage("Chọn chi nhánh");
        RuleFor(command => command.SupplierId).NotEmpty().WithMessage("Chọn nhà cung cấp");
        RuleFor(command => command.Note).MaximumLength(500);
        RuleFor(command => command.Items).NotEmpty().WithMessage("Phiếu cần ít nhất một dòng");
        // UC-INV-02 AC-6.
        RuleForEach(command => command.Items).ChildRules(line =>
        {
            line.RuleFor(item => item.SkuId).NotEmpty().WithMessage("Chọn SKU");
            line.RuleFor(item => item.Quantity).GreaterThan(0).WithMessage("Số lượng phải dương");
            line.RuleFor(item => item.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm");
        });
    }
}

// UC-INV-02 AC-1: lưu phiếu nhập ở Draft; tồn chưa bị tác động tới khi xác nhận.
public sealed class UpsertPurchaseReceiptHandler(
    IUnitOfWork unitOfWork,
    IPurchaseReceiptRepository receipts,
    IReferenceRepository references,
    IStockQueries queries,
    IClock clock)
{
    private static readonly UpsertPurchaseReceiptValidator Validator = new();

    public async Task<PurchaseReceiptDto> Handle(UpsertPurchaseReceiptCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        PurchaseReceipt? receipt = null;
        if (command.Id is { } id)
            receipt = await receipts.GetForUpdateAsync(id, ct) ?? throw new NotFoundException("phiếu nhập");

        if (!await receipts.SupplierExistsAsync(command.SupplierId, ct))
            throw new NotFoundException("nhà cung cấp");

        // SKU không có trong sku_refs bị từ chối ngay ở Draft, không đợi tới lúc xác nhận.
        var known = (await references.ListSkusAsync(command.Items.Select(item => item.SkuId).ToHashSet(), ct))
            .Select(sku => sku.SkuId).ToHashSet();
        if (command.Items.FirstOrDefault(item => !known.Contains(item.SkuId)) is { } unknown)
            throw new ReferenceNotReadyException("SKU", unknown.SkuId);

        if (receipt is null)
        {
            var branch = await references.FindBranchAsync(command.BranchId, ct)
                ?? throw new ReferenceNotReadyException("Chi nhánh", command.BranchId);
            branch.EnsureActive();

            receipt = PurchaseReceipt.Create(command.BranchId, command.SupplierId, command.Note, command.Items, clock.UtcNow);
            receipts.Add(receipt);
        }
        else
        {
            receipt.Revise(command.SupplierId, command.Note, command.Items);
        }

        await transaction.CommitAsync(ct);
        return (await queries.FindPurchaseReceiptAsync(receipt.Id, ct))!;
    }
}
