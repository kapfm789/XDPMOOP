using Oism.SharedKernel;

namespace Oism.Insights.Domain.Sales;

// Tổng của sales_facts trong một ngày giờ Việt Nam theo chi nhánh, kênh và SKU
// (docs/design/data-model/insights.md mục "Các bảng còn lại"). Job đêm dựng lại cả ngày, không cộng dồn.
public sealed class DailyAggregate : ITenantOwned
{
    private DailyAggregate()
    {
    }

    public Guid TenantId { get; private set; }

    public DateOnly Date { get; private set; }

    public Guid BranchId { get; private set; }

    public string Channel { get; private set; } = null!;

    public Guid SkuId { get; private set; }

    public int Quantity { get; private set; }

    public decimal Revenue { get; private set; }

    public decimal Cogs { get; private set; }

    public static DailyAggregate Of(
        DateOnly date, Guid branchId, string channel, Guid skuId, int quantity, decimal revenue, decimal cogs) => new()
    {
        Date = date,
        BranchId = branchId,
        Channel = channel,
        SkuId = skuId,
        Quantity = quantity,
        Revenue = revenue,
        Cogs = cogs,
    };
}
