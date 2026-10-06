using Oism.SharedKernel;

namespace Oism.Core.Domain.Inventory;

public enum StockTransferStatus
{
    Draft,
    InTransit,
    Received,
}

public sealed record TransferLine(Guid SkuId, int Quantity);

// Phiếu chuyển kho: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ"; trạng thái ở docs/design/state-machines.md.
// Giữa lúc xuất và lúc nhận, hàng không thuộc tồn của chi nhánh nào mà nằm trên phiếu InTransit (ADR-0009).
public sealed class StockTransfer : ITenantOwned
{
    private readonly List<StockTransferItem> _items = [];

    private StockTransfer()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid FromBranchId { get; private set; }

    public Guid ToBranchId { get; private set; }

    public string TransferNumber { get; private set; } = null!;

    public StockTransferStatus Status { get; private set; }

    public DateTimeOffset? ShippedAt { get; private set; }

    public DateTimeOffset? ReceivedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public IReadOnlyCollection<StockTransferItem> Items => _items;

    public static StockTransfer Create(Guid fromBranchId, Guid toBranchId, IEnumerable<TransferLine> lines, Guid? createdBy)
    {
        // UC-INV-03 AC-7.
        if (fromBranchId == toBranchId)
            throw new ArgumentException("A transfer needs two different branches.", nameof(toBranchId));

        var id = Guid.NewGuid();
        var transfer = new StockTransfer
        {
            Id = id,
            FromBranchId = fromBranchId,
            ToBranchId = toBranchId,
            // ponytail: mã hiển thị lấy từ id của phiếu, như mã đơn; cần mã liền mạch thì thêm bộ đếm theo tenant.
            TransferNumber = $"CK{id.ToString("N")[..12].ToUpperInvariant()}",
            Status = StockTransferStatus.Draft,
            CreatedBy = createdBy,
        };
        transfer._items.AddRange(lines.Select(line => StockTransferItem.Create(id, line)));
        return transfer;
    }

    // Chỉ xóa được phiếu khi còn Draft; đã xuất thì hàng đang trên đường và không hủy được (ADR-0009).
    // Gọi sau khi đã khóa dòng phiếu.
    public void EnsureCanDelete()
    {
        if (Status != StockTransferStatus.Draft)
            throw new InvalidStateTransitionException("phiếu chuyển kho", Status.ToString(), "Deleted");
    }

    // Draft sang InTransit. Gọi sau khi đã khóa dòng phiếu, trước khi ghi sổ OUT ở nơi gửi.
    public void Ship(DateTimeOffset now)
    {
        if (Status != StockTransferStatus.Draft)
            throw new InvalidStateTransitionException("phiếu chuyển kho", Status.ToString(), StockTransferStatus.InTransit.ToString());

        Status = StockTransferStatus.InTransit;
        ShippedAt = now;
    }

    // Giá vốn nơi gửi lúc xuất, tra theo Id của dòng phiếu; nơi nhận dùng nó để tính lại giá vốn bình quân (ADR-0004).
    public void SnapshotCosts(IReadOnlyDictionary<Guid, decimal> unitCosts)
    {
        foreach (var item in _items)
            item.FixUnitCost(unitCosts[item.Id]);
    }

    // False khi phiếu đã Received: nhận lại không tác động lần hai (UC-INV-03 AC-6). Phiếu còn Draft thì ném lỗi.
    // Gọi sau khi đã khóa dòng phiếu.
    public bool Receive(DateTimeOffset now)
    {
        if (Status == StockTransferStatus.Received)
            return false;
        if (Status != StockTransferStatus.InTransit)
            throw new InvalidStateTransitionException("phiếu chuyển kho", Status.ToString(), StockTransferStatus.Received.ToString());

        Status = StockTransferStatus.Received;
        ReceivedAt = now;
        return true;
    }
}

public sealed class StockTransferItem : ITenantOwned
{
    private StockTransferItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TransferId { get; private set; }

    public Guid SkuId { get; private set; }

    public int Quantity { get; private set; }

    // Ghi một lần lúc xuất; null khi phiếu còn Draft.
    public decimal? UnitCost { get; private set; }

    internal static StockTransferItem Create(Guid transferId, TransferLine line)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(line.Quantity);

        return new StockTransferItem
        {
            Id = Guid.NewGuid(),
            TransferId = transferId,
            SkuId = line.SkuId,
            Quantity = line.Quantity,
        };
    }

    internal void FixUnitCost(decimal unitCost)
    {
        if (UnitCost is not null)
            throw new InvalidOperationException("Unit cost is already fixed.");

        UnitCost = unitCost;
    }
}
