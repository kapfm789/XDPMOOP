namespace Oism.Core.Domain.Inventory;

// Giá vốn bình quân theo chi nhánh và SKU: docs/decisions/0004-wac-per-branch.md.
public static class WeightedAverageCost
{
    // onHand là tồn trước khi nhập, gồm cả phần đang giữ; tồn 0 thì giá vốn mới bằng đúng đơn giá nhập.
    // Làm tròn 4 chữ số thập phân, nửa lên, theo numeric(18,4).
    public static decimal Recalculate(int onHand, decimal avgCost, int quantity, decimal unitCost) =>
        Math.Round(((onHand * avgCost) + (quantity * unitCost)) / (onHand + quantity), 4, MidpointRounding.AwayFromZero);
}
