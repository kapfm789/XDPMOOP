using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory;

public interface IStocktakeRepository
{
    // Khóa dòng phiên bằng SELECT ... FOR UPDATE rồi nạp các dòng của nó. Phải gọi trong transaction của use case,
    // trước mọi kiểm tra trạng thái và trước khi khóa số dư. Null khi phiên không có trong tenant.
    Task<Stocktake?> GetForUpdateAsync(Guid id, CancellationToken ct);

    void Add(Stocktake stocktake);

    // Các đơn đang có phần giữ Active trên những SKU này tại chi nhánh, tra theo SkuId, đơn cũ nhất trước.
    Task<ILookup<Guid, HeldOrder>> ListHeldOrdersAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct);
}
