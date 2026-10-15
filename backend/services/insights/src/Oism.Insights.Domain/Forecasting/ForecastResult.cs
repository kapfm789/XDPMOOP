using Oism.SharedKernel;

namespace Oism.Insights.Domain.Forecasting;

// Kết quả dự báo của một SKU tại một chi nhánh trong một lần chạy. Chưa đủ dữ liệu thì ba trường số để trống
// (docs/design/data-model/insights.md mục "Các bảng còn lại").
public sealed class ForecastResult : ITenantOwned
{
    private ForecastResult()
    {
    }

    public Guid RunId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid SkuId { get; private set; }

    public bool HasEnoughData { get; private set; }

    // Nhu cầu dự báo mỗi ngày: mức cuối của san bằng hàm mũ.
    public decimal? DailyLevel { get; private set; }

    // Nhu cầu dự báo trong HorizonDays ngày.
    public decimal? ForecastQty { get; private set; }

    public int? SuggestedReorderQty { get; private set; }

    public static ForecastResult NotEnoughData(Guid runId, Guid branchId, Guid skuId) => new()
    {
        RunId = runId,
        BranchId = branchId,
        SkuId = skuId,
        HasEnoughData = false,
    };

    public static ForecastResult Of(
        Guid runId, Guid branchId, Guid skuId, decimal dailyLevel, decimal forecastQty, int suggestedReorderQty) => new()
    {
        RunId = runId,
        BranchId = branchId,
        SkuId = skuId,
        HasEnoughData = true,
        DailyLevel = dailyLevel,
        ForecastQty = forecastQty,
        SuggestedReorderQty = suggestedReorderQty,
    };
}
