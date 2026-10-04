namespace Oism.Contracts;

// docs/design/events.md mục "StockChanged". Thông điệp trạng thái: mang số dư sau thay đổi;
// Version bằng inventory_balances.version.
public sealed record StockChanged(
    Guid BranchId, Guid SkuId, int OnHand, int Reserved, int Available, decimal AvgCost, int? Threshold, long Version);
