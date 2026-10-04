using FluentValidation;
using Oism.Core.Application.References;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.Inventory.SetThreshold;

// Threshold null là bỏ ngưỡng. IncludeCost: chỉ Owner thấy giá vốn bình quân trong dòng số dư trả về.
public sealed record SetThresholdCommand(Guid BranchId, Guid SkuId, int? Threshold, bool IncludeCost);

public sealed class SetThresholdValidator : AbstractValidator<SetThresholdCommand>
{
    public SetThresholdValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(command => command.SkuId).NotEmpty().WithMessage("Chọn SKU");
        // UC-INV-05 AC-3.
        RuleFor(command => command.Threshold).GreaterThanOrEqualTo(0).WithMessage("Ngưỡng không được âm");
    }
}

// UC-INV-05: đặt ngưỡng tồn tối thiểu cho một SKU tại một chi nhánh; mỗi lần đặt phát StockChanged mang ngưỡng mới.
public sealed class SetThresholdHandler(
    IUnitOfWork unitOfWork, IReferenceRepository references, IInventoryRepository inventory, IEventPublisher events, IClock clock)
{
    private static readonly SetThresholdValidator Validator = new();

    public async Task<StockDto> Handle(SetThresholdCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        // Tra trong tenant hiện tại trước khi tạo dòng số dư, để không dựng số dư cho ID của tenant khác.
        if (await references.FindBranchAsync(command.BranchId, ct) is null)
            throw new ReferenceNotReadyException("Chi nhánh", command.BranchId);
        var sku = await references.FindSkuAsync(command.SkuId, ct)
            ?? throw new ReferenceNotReadyException("SKU", command.SkuId);

        var balance = (await inventory.LockBalancesAsync(command.BranchId, [command.SkuId], ct))[command.SkuId];
        balance.SetThreshold(command.Threshold, clock.UtcNow);
        events.EnqueueStockChanged(balance);

        await transaction.CommitAsync(ct);
        return new StockDto(
            balance.BranchId, balance.SkuId, sku.SkuCode, sku.Name, balance.OnHand, balance.Reserved, balance.Available,
            command.IncludeCost ? balance.AvgCost : null, balance.ReorderThreshold);
    }
}
