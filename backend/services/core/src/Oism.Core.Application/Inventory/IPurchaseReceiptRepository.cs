using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory;

public interface IPurchaseReceiptRepository
{
    // Khóa dòng phiếu bằng SELECT ... FOR UPDATE rồi nạp các dòng của nó. Phải gọi trong transaction của use case,
    // trước mọi kiểm tra trạng thái và trước khi khóa số dư. Null khi phiếu không có trong tenant.
    Task<PurchaseReceipt?> GetForUpdateAsync(Guid id, CancellationToken ct);

    void Add(PurchaseReceipt receipt);

    Task<bool> SupplierExistsAsync(Guid supplierId, CancellationToken ct);

    void Add(Supplier supplier);
}
