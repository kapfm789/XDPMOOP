using Oism.SharedKernel;

namespace Oism.Core.Domain.Inventory;

// Số dư của một SKU tại một chi nhánh: docs/design/data-model/core.md mục "inventory_balances".
// Đây là dòng bị khóa khi đổi tồn (docs/architecture/transactions-and-concurrency.md).
public sealed class InventoryBalance(Guid branchId, Guid skuId) : ITenantOwned
{
    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; } = branchId;

    public Guid SkuId { get; private set; } = skuId;

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public decimal AvgCost { get; private set; }

    public int? ReorderThreshold { get; private set; }

    // Tăng 1 mỗi lần dòng đổi; đi kèm StockChanged.
    public long Version { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int Available => OnHand - Reserved;

    // Cửa duy nhất đổi OnHand: mỗi lần đổi trả về đúng một dòng sổ mang số dư sau giao dịch.
    // Gọi khi đang giữ khóa dòng số dư, để BalanceAfter nhất quán theo seq.
    public InventoryTransaction Post(
        LedgerType type,
        LedgerReason reason,
        int quantity,
        decimal unitCost,
        LedgerReference referenceType,
        Guid referenceId,
        Guid? createdBy,
        DateTimeOffset now,
        Guid? reversalOfId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(unitCost);

        // Hàng đang được giữ không xuất được: sau giao dịch vẫn phải có reserved <= on_hand.
        if (type == LedgerType.OUT && quantity > Available)
            throw new InsufficientStockException([new StockShortage(SkuId, quantity, Available)]);

        // Nhập mua và nhận chuyển kho tính lại giá vốn bình quân trên tồn trước khi nhập (ADR-0004).
        // Mọi bút toán khác, kể cả điều chỉnh kiểm kê, giữ nguyên giá vốn.
        if (type == LedgerType.IN && reason is LedgerReason.Purchase or LedgerReason.TransferIn)
            AvgCost = WeightedAverageCost.Recalculate(OnHand, AvgCost, quantity, unitCost);

        OnHand += type == LedgerType.IN ? quantity : -quantity;
        Version++;
        UpdatedAt = now;

        return new InventoryTransaction(
            TenantId, BranchId, SkuId, type, reason, quantity, OnHand, unitCost, referenceType, referenceId, reversalOfId, createdBy, now);
    }

    // Giữ hàng: tăng Reserved, không đổi OnHand nên không ghi sổ.
    // Chỉ IStockService gọi, khi đang giữ khóa dòng số dư.
    public void Reserve(int quantity, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (quantity > Available)
            throw new InsufficientStockException([new StockShortage(SkuId, quantity, Available)]);

        Reserved += quantity;
        Version++;
        UpdatedAt = now;
    }

    // UC-INV-05: ngưỡng tồn tối thiểu; null là không cảnh báo. Gọi khi đang giữ khóa dòng số dư.
    public void SetThreshold(int? threshold, DateTimeOffset now)
    {
        if (threshold is { } value)
            ArgumentOutOfRangeException.ThrowIfNegative(value);

        ReorderThreshold = threshold;
        Version++;
        UpdatedAt = now;
    }
}
