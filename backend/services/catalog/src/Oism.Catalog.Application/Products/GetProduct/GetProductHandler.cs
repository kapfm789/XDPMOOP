using Oism.SharedKernel;

namespace Oism.Catalog.Application.Products.GetProduct;

public sealed class GetProductHandler(IProductRepository products)
{
    public async Task<ProductDto> Handle(Guid id, CancellationToken ct) =>
        ProductDto.From(await products.GetAsync(id, ct) ?? throw new NotFoundException("sản phẩm"));
}
