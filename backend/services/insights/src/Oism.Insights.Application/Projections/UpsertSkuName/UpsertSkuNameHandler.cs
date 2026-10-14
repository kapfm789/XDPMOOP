using Oism.Contracts;
using Oism.Insights.Domain.References;

namespace Oism.Insights.Application.Projections.UpsertSkuName;

// Ghi đè sku_names theo SkuUpserted; bản có version nhỏ hơn hoặc bằng bản đang giữ bị bỏ qua.
// Không mở transaction: consumer base đã mở, ghi inbox và sẽ commit.
public sealed class UpsertSkuNameHandler(IProjectionRepository projections)
{
    public async Task Handle(SkuUpserted message, CancellationToken ct)
    {
        var sku = await projections.FindSkuNameAsync(message.SkuId, ct);
        if (sku is null)
            projections.Add(sku = new SkuName(message.SkuId));

        sku.Apply(message.SkuCode, message.Name, message.Version);
    }
}
