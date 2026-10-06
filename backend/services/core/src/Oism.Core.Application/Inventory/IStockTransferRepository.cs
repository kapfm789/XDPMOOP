using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory;

public interface IStockTransferRepository
{
    // Khóa dòng phiếu bằng SELECT ... FOR UPDATE rồi nạp các dòng của nó. Phải gọi trong transaction của use case,
    // trước mọi kiểm tra trạng thái và trước khi khóa số dư. Null khi phiếu không có trong tenant.
    Task<StockTransfer?> GetForUpdateAsync(Guid id, CancellationToken ct);

    void Add(StockTransfer transfer);

    // Xóa phiếu cùng các dòng của nó.
    void Remove(StockTransfer transfer);
}
