using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.Inventory;

// SystemQty và Difference chỉ có sau khi chốt.
public sealed record StocktakeItemDto(
    Guid Id, Guid SkuId, string SkuCode, string SkuName, int CountedQty, int? SystemQty, int? Difference);

// Phiên kiểm kê kèm các dòng: docs/design/api/core.md mục "Chuyển kho và kiểm kê".
public sealed record StocktakeDto(
    Guid Id, Guid BranchId, string Status, Guid? CreatedBy, DateTimeOffset? PostedAt, IReadOnlyList<StocktakeItemDto> Items)
{
    // Mã và tên SKU lấy từ sku_refs lúc đọc; `skus` tra theo SkuId.
    public static StocktakeDto From(Stocktake stocktake, IReadOnlyDictionary<Guid, SkuRef> skus) => new(
        stocktake.Id, stocktake.BranchId, stocktake.Status.ToString(), stocktake.CreatedBy, stocktake.PostedAt,
        stocktake.Items
            .Select(item => new StocktakeItemDto(
                item.Id, item.SkuId, skus[item.SkuId].SkuCode, skus[item.SkuId].Name, item.CountedQty, item.SystemQty, item.Difference))
            .OrderBy(item => item.SkuCode)
            .ToList());
}

public static class StocktakeDtos
{
    public static async Task<StocktakeDto> ToDtoAsync(this IReferenceRepository references, Stocktake stocktake, CancellationToken ct)
    {
        var skus = await references.ListSkusAsync(stocktake.Items.Select(item => item.SkuId).ToHashSet(), ct);
        return StocktakeDto.From(stocktake, skus.ToDictionary(sku => sku.SkuId));
    }
}
