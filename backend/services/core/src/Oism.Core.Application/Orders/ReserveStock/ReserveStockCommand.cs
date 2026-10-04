using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Orders.ReserveStock;

// UnitPrice bỏ trống thì dùng giá lẻ hiện tại của SKU; Discount bỏ trống là 0.
public sealed record ReserveStockLine(Guid SkuId, int Quantity, decimal? UnitPrice, decimal? Discount);

// Tạo đơn và giữ hàng: docs/design/flows/reserve-stock.md. CreatedBy null và ExternalOrderId có giá trị với đơn từ sàn.
public sealed record ReserveStockCommand(
    Guid BranchId,
    OrderChannel Channel,
    string? Note,
    IReadOnlyList<ReserveStockLine> Items,
    Guid? CreatedBy,
    string? ExternalOrderId = null);
