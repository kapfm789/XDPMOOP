using System.Text.Json.Serialization;
using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.Inventory;

// UnitCost là giá vốn nơi gửi lúc xuất: chỉ có sau khi xuất và chỉ trả cho Owner.
public sealed record TransferItemDto(
    Guid Id,
    Guid SkuId,
    string SkuCode,
    string SkuName,
    int Quantity,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? UnitCost);

// Phiếu chuyển kho kèm các dòng: docs/design/api/core.md mục "Chuyển kho và kiểm kê".
public sealed record TransferDto(
    Guid Id,
    string TransferNumber,
    Guid FromBranchId,
    Guid ToBranchId,
    string Status,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? ReceivedAt,
    Guid? CreatedBy,
    IReadOnlyList<TransferItemDto> Items)
{
    // Mã và tên SKU lấy từ sku_refs lúc đọc; `skus` tra theo SkuId.
    public static TransferDto From(StockTransfer transfer, IReadOnlyDictionary<Guid, SkuRef> skus, bool includeCost) => new(
        transfer.Id, transfer.TransferNumber, transfer.FromBranchId, transfer.ToBranchId, transfer.Status.ToString(),
        transfer.ShippedAt, transfer.ReceivedAt, transfer.CreatedBy,
        transfer.Items
            .Select(item => new TransferItemDto(
                item.Id, item.SkuId, skus[item.SkuId].SkuCode, skus[item.SkuId].Name, item.Quantity,
                includeCost ? item.UnitCost : null))
            .OrderBy(item => item.SkuCode).ThenBy(item => item.Id)
            .ToList());
}

public static class TransferDtos
{
    public static async Task<TransferDto> ToDtoAsync(
        this IReferenceRepository references, StockTransfer transfer, bool includeCost, CancellationToken ct)
    {
        var skus = await references.ListSkusAsync(transfer.Items.Select(item => item.SkuId).ToHashSet(), ct);
        return TransferDto.From(transfer, skus.ToDictionary(sku => sku.SkuId), includeCost);
    }
}
