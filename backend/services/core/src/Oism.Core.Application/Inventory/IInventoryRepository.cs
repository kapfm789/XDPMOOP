using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Inventory;

public interface IInventoryRepository
{
    // Tạo dòng số dư còn thiếu, rồi khóa mọi dòng số dư của các SKU tại chi nhánh trong một câu lệnh,
    // theo branch_id rồi sku_id tăng dần (docs/architecture/transactions-and-concurrency.md mục "Thứ tự khóa").
    // Phải gọi trong transaction của use case, sau khi đã khóa chứng từ. Kết quả tra theo SkuId.
    Task<IReadOnlyDictionary<Guid, InventoryBalance>> LockBalancesAsync(
        Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct);

    void Add(InventoryTransaction entry);

    void Add(Reservation reservation);
}
