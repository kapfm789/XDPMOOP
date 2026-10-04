using Oism.SharedKernel;

namespace Oism.Core.Domain.Inventory;

public enum PurchaseReceiptStatus
{
    Draft,
    Confirmed,
}

// Một dòng của phiếu nhập: số lượng và đơn giá nhập trên phiếu.
public sealed record PurchaseLine(Guid SkuId, int Quantity, decimal UnitCost);

// Phiếu nhập: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ"; trạng thái ở docs/design/state-machines.md.
// Tồn chỉ đổi khi phiếu sang Confirmed (docs/design/flows/purchase-receipt.md).
public sealed class PurchaseReceipt : ITenantOwned
{
    private readonly List<PurchaseReceiptItem> _items = [];

    private PurchaseReceipt()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid SupplierId { get; private set; }

    public string ReceiptNumber { get; private set; } = null!;

    public PurchaseReceiptStatus Status { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public Guid? ConfirmedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<PurchaseReceiptItem> Items => _items;

    public static PurchaseReceipt Create(
        Guid branchId, Guid supplierId, string? note, IEnumerable<PurchaseLine> lines, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        var receipt = new PurchaseReceipt
        {
            Id = id,
            BranchId = branchId,
            // ponytail: mã hiển thị lấy từ id của phiếu, như mã đơn; cần mã liền mạch thì thêm bộ đếm theo tenant.
            ReceiptNumber = $"PN{id.ToString("N")[..12].ToUpperInvariant()}",
            Status = PurchaseReceiptStatus.Draft,
            CreatedAt = now,
        };
        receipt.Revise(supplierId, note, lines);
        return receipt;
    }

    // UC-INV-02 AC-1: phiếu Draft sửa và xóa dòng được, tồn chưa bị tác động. Phiếu Confirmed không sửa được.
    // Gọi sau khi đã khóa dòng phiếu.
    public void Revise(Guid supplierId, string? note, IEnumerable<PurchaseLine> lines)
    {
        if (Status != PurchaseReceiptStatus.Draft)
            throw new InvalidStateTransitionException("phiếu nhập", Status.ToString(), PurchaseReceiptStatus.Draft.ToString());

        SupplierId = supplierId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        _items.Clear();
        _items.AddRange(lines.Select(line => PurchaseReceiptItem.Create(Id, line)));
    }

    // False khi phiếu đã Confirmed: xác nhận lại không tác động lần hai (UC-INV-02 AC-5).
    // Gọi sau khi đã khóa dòng phiếu.
    public bool Confirm(Guid? confirmedBy, DateTimeOffset now)
    {
        if (Status == PurchaseReceiptStatus.Confirmed)
            return false;

        Status = PurchaseReceiptStatus.Confirmed;
        ConfirmedAt = now;
        ConfirmedBy = confirmedBy;
        return true;
    }
}

public sealed class PurchaseReceiptItem : ITenantOwned
{
    private PurchaseReceiptItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ReceiptId { get; private set; }

    public Guid SkuId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    internal static PurchaseReceiptItem Create(Guid receiptId, PurchaseLine line)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(line.Quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(line.UnitCost);

        return new PurchaseReceiptItem
        {
            Id = Guid.NewGuid(),
            ReceiptId = receiptId,
            SkuId = line.SkuId,
            Quantity = line.Quantity,
            UnitCost = line.UnitCost,
        };
    }
}
