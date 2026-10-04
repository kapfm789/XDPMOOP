using Oism.Contracts;
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
}

public sealed class StockService(IInventoryRepository inventory, IEventPublisher events, IClock clock) : IStockService
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
}

public static class StockEvents
{
    // StockChanged mang toàn bộ trạng thái của dòng số dư sau thay đổi (docs/design/events.md).
    public static void EnqueueStockChanged(this IEventPublisher events, InventoryBalance balance) =>
        events.Enqueue(new StockChanged(
            balance.BranchId, balance.SkuId, balance.OnHand, balance.Reserved, balance.Available, balance.AvgCost,
            balance.ReorderThreshold, balance.Version));
}
