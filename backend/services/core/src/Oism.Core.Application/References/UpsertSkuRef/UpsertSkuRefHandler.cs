using Oism.Contracts;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.References.UpsertSkuRef;

// Ghi đè bản sao SKU theo SkuUpserted. Không mở transaction: phần chung của consumer đã mở,
// ghi inbox và sẽ commit (docs/architecture/messaging.md mục "Dùng trong một service").
public sealed class UpsertSkuRefHandler(IReferenceRepository references)
{
    public async Task Handle(SkuUpserted message, CancellationToken ct)
    {
        var sku = await references.FindSkuAsync(message.SkuId, ct);
        if (sku is null)
            references.Add(sku = new SkuRef(message.SkuId));

        sku.Apply(
            message.SkuCode, message.Name, message.Barcodes, message.RetailPrice, message.WholesalePrice, message.IsActive,
            message.Version);
    }
}
