using Oism.SharedKernel;

namespace Oism.Core.Domain.Orders;

// Bảng: docs/design/data-model/core.md mục "order_items".
public sealed class OrderItem : ITenantOwned
{
    private OrderItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid SkuId { get; private set; }

    public string SkuCode { get; private set; } = null!;

    public string SkuName { get; private set; } = null!;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal Discount { get; private set; }

    // Ghi một lần lúc xác nhận đơn; null trước đó.
    public decimal? CostPrice { get; private set; }

    public decimal LineTotal => (Quantity * UnitPrice) - Discount;

    // Giảm giá của dòng không âm và không vượt thành tiền của dòng.
    public static bool IsValidDiscount(int quantity, decimal unitPrice, decimal discount) =>
        discount >= 0 && discount <= quantity * unitPrice;

    internal static OrderItem Create(
        Guid orderId, Guid skuId, string skuCode, string skuName, int quantity, decimal unitPrice, decimal discount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        if (!IsValidDiscount(quantity, unitPrice, discount))
            throw new ArgumentOutOfRangeException(nameof(discount));

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            SkuId = skuId,
            SkuCode = skuCode,
            SkuName = skuName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Discount = discount,
        };
    }
}
