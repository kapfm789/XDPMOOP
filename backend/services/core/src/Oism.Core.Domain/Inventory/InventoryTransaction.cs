using Oism.SharedKernel;

namespace Oism.Core.Domain.Inventory;

public enum LedgerType
{
    IN,
    OUT,
}

public enum LedgerReason
{
    Purchase,
    Sale,
    TransferOut,
    TransferIn,
    StocktakeAdjust,
    ReturnIn,
    Reversal,
}

// Loại chứng từ nguồn của một dòng sổ.
public enum LedgerReference
{
    PurchaseReceipt,
    Order,
    StockTransfer,
    Stocktake,
}

// Một dòng của sổ giao dịch: docs/design/data-model/core.md mục "inventory_transactions".
// Sổ chỉ thêm mới: lớp này không có phương thức sửa, và chỉ InventoryBalance.Post tạo được dòng mới.
// Sửa sai bằng một dòng lý do Reversal trỏ về dòng gốc qua ReversalOfId (NFR-SEC-03).
public sealed class InventoryTransaction : ITenantOwned
{
    private InventoryTransaction()
    {
    }

    internal InventoryTransaction(
        Guid tenantId,
        Guid branchId,
        Guid skuId,
        LedgerType type,
        LedgerReason reason,
        int quantity,
        int balanceAfter,
        decimal unitCost,
        LedgerReference referenceType,
        Guid referenceId,
        Guid? reversalOfId,
        Guid? createdBy,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        BranchId = branchId;
        SkuId = skuId;
        Type = type;
        Reason = reason;
        Quantity = quantity;
        BalanceAfter = balanceAfter;
        UnitCost = unitCost;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        ReversalOfId = reversalOfId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    // Thứ tự ghi, do database cấp. Thứ tự của sổ là theo seq, không theo CreatedAt.
    public long Seq { get; private set; }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid SkuId { get; private set; }

    public LedgerType Type { get; private set; }

    public LedgerReason Reason { get; private set; }

    // Luôn dương; chiều tăng giảm nằm ở Type.
    public int Quantity { get; private set; }

    // OnHand sau giao dịch, tính dưới khóa dòng số dư.
    public int BalanceAfter { get; private set; }

    // Giá nhập, hoặc giá vốn lúc xuất.
    public decimal UnitCost { get; private set; }

    public LedgerReference ReferenceType { get; private set; }

    public Guid ReferenceId { get; private set; }

    public Guid? ReversalOfId { get; private set; }

    // Null khi do job hệ thống.
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
