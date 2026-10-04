using Oism.SharedKernel;

namespace Oism.Catalog.Application.Products.RemoveBarcode;

public sealed record RemoveBarcodeCommand(Guid SkuId, Guid BarcodeId);

public sealed class RemoveBarcodeHandler(IUnitOfWork unitOfWork, IProductRepository products, IEventPublisher events)
{
    public async Task Handle(RemoveBarcodeCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var product = await products.GetBySkuForUpdateAsync(command.SkuId, ct) ?? throw new NotFoundException("SKU");
        var sku = product.Skus.Single(candidate => candidate.Id == command.SkuId);
        if (!sku.RemoveBarcode(command.BarcodeId))
            throw new NotFoundException("mã vạch");
        events.EnqueueSkuUpserted(product, sku);

        await transaction.CommitAsync(ct);
    }
}
