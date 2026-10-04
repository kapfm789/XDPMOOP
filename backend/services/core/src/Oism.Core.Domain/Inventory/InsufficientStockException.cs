using Oism.SharedKernel;

namespace Oism.Core.Domain.Inventory;

// Một phần tử của `details`: docs/design/api/README.md mục "Lỗi".
public sealed record StockShortage(Guid SkuId, int Requested, int Available);

public sealed class InsufficientStockException(IReadOnlyList<StockShortage> shortages)
    : OismException("insufficient_stock", "Không đủ tồn khả dụng", shortages);
