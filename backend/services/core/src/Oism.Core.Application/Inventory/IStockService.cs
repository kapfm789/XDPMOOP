using Oism.Contracts;
using Oism.Core.Application.Inventory.PostLedger;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Inventory;

// Cửa duy nhất đổi `reserved` (docs/architecture/transactions-and-concurrency.md mục "Một cửa duy nhất để đổi tồn").
// Module Orders chỉ đổi tồn qua interface này. Mọi phương thức chạy trong transaction của use case gọi nó,
// sau khi use case đó đã khóa hoặc chèn dòng đơn.
public interface IStockService
{
    // Giữ hàng cho mọi dòng của đơn, hoặc ném InsufficientStockException nêu mọi SKU thiếu mà không giữ dòng nào.
    // Không ghi sổ vì on_hand không đổi.
    Task ReserveAsync(Order order, DateTimeOffset expiresAt, CancellationToken ct);

    // Xuất hàng cho đơn: mỗi phần giữ Active giảm `reserved`, ghi một dòng sổ OUT lý do Sale qua PostLedger
    // và sang Consumed. Trả giá vốn bình quân đã dùng, tra theo Id của dòng đơn.
    Task<IReadOnlyDictionary<Guid, decimal>> ConsumeAsync(Order order, Guid? createdBy, CancellationToken ct);

    // Trả hàng đã giữ về tồn khả dụng: mỗi phần giữ Active giảm `reserved` và sang Released. Không ghi sổ.
    Task ReleaseAsync(Order order, CancellationToken ct);
}

public sealed class StockService(
    IInventoryRepository inventory, PostLedgerHandler postLedger, IEventPublisher events, IClock clock) : IStockService
{
    public async Task ReserveAsync(Order order, DateTimeOffset expiresAt, CancellationToken ct)
    {
        // Một SKU xuất hiện ở hai dòng đơn: cộng số lượng lại trước khi kiểm.
        var requested = order.Items
            .GroupBy(item => item.SkuId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        // SKU chưa có dòng số dư ở chi nhánh được tạo ở 0, tức tồn khả dụng bằng 0.
        var balances = await inventory.LockBalancesAsync(order.BranchId, requested.Keys, ct);

        // Kiểm mọi SKU dưới khóa trước khi đổi bất kỳ số dư nào: giữ toàn bộ hoặc từ chối toàn bộ (ADR-0005).
        var shortages = requested
            .Where(line => balances[line.Key].Available < line.Value)
            .Select(line => new StockShortage(line.Key, line.Value, balances[line.Key].Available))
            .ToList();
        if (shortages.Count > 0)
            throw new InsufficientStockException(shortages);

        var now = clock.UtcNow;
        foreach (var item in order.Items)
        {
            balances[item.SkuId].Reserve(item.Quantity, now);
            inventory.Add(Reservation.Hold(order.BranchId, item, expiresAt, now));
        }

        foreach (var balance in balances.Values)
            events.EnqueueStockChanged(balance);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> ConsumeAsync(Order order, Guid? createdBy, CancellationToken ct)
    {
        var (holds, balances) = await LockHeldAsync(order, ct);

        // Bỏ giữ trước rồi mới xuất: PostLedger chỉ cho xuất trong tồn khả dụng.
        var now = clock.UtcNow;
        foreach (var hold in holds)
        {
            balances[hold.SkuId].Release(hold.Quantity, now);
            hold.Consume(now);
        }

        // PostLedger khóa lại đúng các dòng số dư đang giữ, ghi sổ và phát StockChanged mang số dư sau khi xuất.
        var posted = await postLedger.Handle(
            new PostLedgerCommand(
                order.BranchId, LedgerReference.Order, order.Id,
                holds.Select(hold => new LedgerEntry(hold.SkuId, LedgerType.OUT, LedgerReason.Sale, hold.Quantity, UnitCost: null)).ToList(),
                createdBy),
            ct);
        return holds.Zip(posted).ToDictionary(pair => pair.First.OrderItemId, pair => pair.Second.UnitCost);
    }

    public async Task ReleaseAsync(Order order, CancellationToken ct)
    {
        var (holds, balances) = await LockHeldAsync(order, ct);

        var now = clock.UtcNow;
        foreach (var hold in holds)
        {
            balances[hold.SkuId].Release(hold.Quantity, now);
            hold.Release(now);
        }

        foreach (var balance in balances.Values)
            events.EnqueueStockChanged(balance);
    }

    private async Task<(IReadOnlyList<Reservation> Holds, IReadOnlyDictionary<Guid, InventoryBalance> Balances)> LockHeldAsync(
        Order order, CancellationToken ct)
    {
        var holds = await inventory.ListActiveReservationsAsync(order.Id, ct);
        var balances = await inventory.LockBalancesAsync(order.BranchId, holds.Select(hold => hold.SkuId).ToHashSet(), ct);
        return (holds, balances);
    }
}

public static class StockEvents
{
    // StockChanged mang toàn bộ trạng thái của dòng số dư sau thay đổi (docs/design/events.md).
    public static void EnqueueStockChanged(this IEventPublisher events, InventoryBalance balance) =>
        events.Enqueue(new StockChanged(
            balance.BranchId, balance.SkuId, balance.OnHand, balance.Reserved, balance.Available, balance.AvgCost,
            balance.ReorderThreshold, balance.Version));
}
