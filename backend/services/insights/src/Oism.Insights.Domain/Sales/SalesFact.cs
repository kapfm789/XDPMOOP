using Oism.SharedKernel;

namespace Oism.Insights.Domain.Sales;

// Một dòng đơn đã xác nhận, dựng từ OrderConfirmed (docs/design/data-model/insights.md mục "sales_facts").
// Khóa chính là OrderItemId: cùng dòng đơn tới lại không thành dòng thứ hai.
public sealed class SalesFact : ITenantOwned
{
    private SalesFact()
    {
    }

    public Guid OrderItemId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid SkuId { get; private set; }

    public string Channel { get; private set; } = null!;

    public int Quantity { get; private set; }

    public decimal Revenue { get; private set; }

    public decimal Cogs { get; private set; }

    public DateTimeOffset ConfirmedAt { get; private set; }

    // Công thức theo ADR-0008: doanh thu thuần là quantity × unit_price − discount; giá vốn là quantity × cost_price,
    // với cost_price đã chốt lúc xác nhận. Không tính lại giá vốn ở đây.
    public static SalesFact Record(
        Guid orderItemId, Guid orderId, Guid branchId, Guid skuId, string channel, int quantity, decimal unitPrice, decimal discount,
        decimal costPrice, DateTimeOffset confirmedAt) => new()
    {
        OrderItemId = orderItemId,
        OrderId = orderId,
        BranchId = branchId,
        SkuId = skuId,
        Channel = channel,
        Quantity = quantity,
        Revenue = quantity * unitPrice - discount,
        Cogs = quantity * costPrice,
        ConfirmedAt = confirmedAt,
    };
}
