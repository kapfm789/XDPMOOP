using Oism.SharedKernel;

namespace Oism.Core.Domain.Inventory;

public enum StocktakeStatus
{
    Open,
    Posted,
}

// Số đếm thực tế của một SKU trong phiên.
public sealed record StocktakeCount(Guid SkuId, int CountedQty);

// Phiên kiểm kê: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ"; trạng thái ở docs/design/state-machines.md.
// Nhập số đếm không tác động tồn; chỉ bước chốt mới ghi sổ (docs/design/flows/stocktake.md, ADR-0009).
public sealed class Stocktake : ITenantOwned
{
    private readonly List<StocktakeItem> _items = [];

    private Stocktake()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public StocktakeStatus Status { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }

    public IReadOnlyCollection<StocktakeItem> Items => _items;

    public static Stocktake Open(Guid branchId, Guid? createdBy) => new()
    {
        Id = Guid.NewGuid(),
        BranchId = branchId,
        Status = StocktakeStatus.Open,
        CreatedBy = createdBy,
    };

    // UC-INV-04 AC-1: thay cả bộ số đếm của phiên còn Open; tồn chưa bị tác động. Gọi sau khi đã khóa dòng phiên.
    public void SetCounts(IEnumerable<StocktakeCount> counts)
    {
        if (Status != StocktakeStatus.Open)
            throw new InvalidStateTransitionException("phiên kiểm kê", Status.ToString(), StocktakeStatus.Open.ToString());

        _items.Clear();
        _items.AddRange(counts.Select(count => StocktakeItem.Create(Id, count)));
    }

    // UC-INV-04 AC-4: các dòng có số đếm nhỏ hơn lượng đang giữ cho đơn. Còn dòng nào như vậy thì phiên không chốt được,
    // vì đặt on_hand bằng số đếm sẽ làm tồn khả dụng âm (ADR-0009). `balances` là số dư đọc dưới khóa, tra theo SkuId.
    public IReadOnlyList<StocktakeItem> FindBelowReserved(IReadOnlyDictionary<Guid, InventoryBalance> balances) =>
        _items.Where(item => item.CountedQty < balances[item.SkuId].Reserved).ToList();

    // Open sang Posted: chụp số trên sổ và chênh lệch lên từng dòng. Gọi sau khi đã khóa dòng phiên và các dòng số dư.
    public void Post(IReadOnlyDictionary<Guid, InventoryBalance> balances, DateTimeOffset now)
    {
        if (Status != StocktakeStatus.Open)
            throw new InvalidStateTransitionException("phiên kiểm kê", Status.ToString(), StocktakeStatus.Posted.ToString());

        foreach (var item in _items)
            item.Snapshot(balances[item.SkuId].OnHand);
        Status = StocktakeStatus.Posted;
        PostedAt = now;
    }
}

public sealed class StocktakeItem : ITenantOwned
{
    private StocktakeItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid StocktakeId { get; private set; }

    public Guid SkuId { get; private set; }

    public int CountedQty { get; private set; }

    // on_hand đọc dưới khóa lúc chốt, không phải lúc mở phiên; null khi phiên còn Open.
    public int? SystemQty { get; private set; }

    // CountedQty trừ SystemQty: dương là điều chỉnh tăng, âm là điều chỉnh giảm; null khi phiên còn Open.
    public int? Difference { get; private set; }

    internal static StocktakeItem Create(Guid stocktakeId, StocktakeCount count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count.CountedQty);

        return new StocktakeItem
        {
            Id = Guid.NewGuid(),
            StocktakeId = stocktakeId,
            SkuId = count.SkuId,
            CountedQty = count.CountedQty,
        };
    }

    internal void Snapshot(int onHand)
    {
        SystemQty = onHand;
        Difference = CountedQty - onHand;
    }
}

// Một đơn đang giữ SKU bị đếm thiếu, và phần tử của `details`: docs/design/api/core.md mục "Chuyển kho và kiểm kê".
public sealed record HeldOrder(Guid OrderId, string OrderNumber, string Channel, int Quantity);

public sealed record StocktakeShortfall(Guid SkuId, int CountedQty, int Reserved, IReadOnlyList<HeldOrder> Orders);

// Hệ thống không tự chọn đơn nào bị hủy: nhân viên xử lý các đơn trong danh sách rồi chốt lại (ADR-0009).
public sealed class StocktakeBelowReservedException(IReadOnlyList<StocktakeShortfall> shortfalls)
    : OismException("stocktake_below_reserved", "Số đếm thấp hơn lượng đang giữ cho đơn", shortfalls);
