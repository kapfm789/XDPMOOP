using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Catalog.Application;
using Oism.Catalog.Application.Products;
using Oism.Catalog.Domain;

namespace Oism.Catalog.Infrastructure.Repositories;

internal sealed class ProductRepository(CatalogDbContext db, ITenantContext tenant) : IProductRepository
{
    public async Task<PagedResult<ProductListItemDto>> ListAsync(
        string? query, Guid? categoryId, Guid? brandId, int page, int pageSize, CancellationToken ct)
    {
        var products = db.Products.AsNoTracking()
            .Where(product => categoryId == null || product.CategoryId == categoryId)
            .Where(product => brandId == null || product.BrandId == brandId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            products = products.Where(product =>
                EF.Functions.ILike(product.Name, pattern) || product.Skus.Any(sku => EF.Functions.ILike(sku.SkuCode, pattern)));
        }

        var items = await products
            .OrderBy(product => product.Name).ThenBy(product => product.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(product => new ProductListItemDto(
                product.Id, product.Name, product.CategoryId, product.BrandId, product.HasVariants, product.IsActive,
                product.Skus.Count()))
            .ToListAsync(ct);
        return new PagedResult<ProductListItemDto>(items, page, pageSize, await products.CountAsync(ct));
    }

    public Task<Product?> GetAsync(Guid id, CancellationToken ct) =>
        db.Products.AsNoTracking()
            .Include(product => product.Skus).ThenInclude(sku => sku.Barcodes)
            .SingleOrDefaultAsync(product => product.Id == id, ct);

    // Điều kiện tenant_id nằm ngay trong câu khóa để chỉ khóa dòng của tenant hiện tại;
    // Global Query Filter vẫn được áp ở lớp ngoài.
    public async Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct)
    {
        var product = (await db.Products
            .FromSql($"SELECT * FROM catalog.products WHERE tenant_id = {tenant.TenantId} AND id = {id} FOR UPDATE")
            .ToListAsync(ct))
            .SingleOrDefault();
        if (product is not null)
            await db.Skus.Where(sku => sku.ProductId == id).Include(sku => sku.Barcodes).LoadAsync(ct);
        return product;
    }

    public async Task<Product?> GetBySkuForUpdateAsync(Guid skuId, CancellationToken ct)
    {
        var productId = await db.Skus.Where(sku => sku.Id == skuId).Select(sku => (Guid?)sku.ProductId).SingleOrDefaultAsync(ct);
        return productId is { } id ? await GetForUpdateAsync(id, ct) : null;
    }

    public Task<bool> AnyInCategoryAsync(Guid categoryId, CancellationToken ct) =>
        db.Products.AnyAsync(product => product.CategoryId == categoryId, ct);

    // Số kế tiếp là số lớn nhất đang dùng cộng 1. Mã cùng độ dài và chỉ gồm chữ số, nên so chuỗi cũng là so số.
    public async Task<long> NextEan13SequenceAsync(CancellationToken ct)
    {
        var lockKey = tenant.TenantId.ToString();
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);

        var last = await db.Barcodes
            .Where(barcode => barcode.Symbology == BarcodeSymbology.EAN13 && barcode.Code.StartsWith(Ean13.InternalPrefix))
            .MaxAsync(barcode => (string?)barcode.Code, ct);
        return last is null ? 1 : Ean13.SequenceOf(last) + 1;
    }

    public void Add(Product product) => db.Add(product);
}
