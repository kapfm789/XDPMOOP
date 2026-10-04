using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Catalog.Application;
using Oism.Catalog.Application.Brands;
using Oism.Catalog.Application.Categories;
using Oism.Catalog.Application.Products;
using Oism.Catalog.Infrastructure;
using Oism.Contracts;

namespace Oism.Catalog.IntegrationTests;

internal static class ApiExtensions
{
    public static async Task<CategoryDto> CreateCategoryAsync(this HttpClient client, string name, Guid? parentId = null)
    {
        var response = await client.PostAsJsonAsync("/categories", new { name, parentId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    public static async Task<IReadOnlyList<CategoryNodeDto>> GetCategoryTreeAsync(this HttpClient client) =>
        (await client.GetFromJsonAsync<IReadOnlyList<CategoryNodeDto>>("/categories"))!;

    public static async Task<BrandDto> CreateBrandAsync(this HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/brands", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BrandDto>())!;
    }

    public static async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(this HttpClient client) =>
        (await client.GetFromJsonAsync<IReadOnlyList<BrandDto>>("/brands"))!;

    // Một phần tử của `skus[]` khi tạo sản phẩm, cũng là body của POST /products/{id}/skus.
    public static object Sku(string skuCode, object? attributes = null, decimal? retailPrice = null, decimal? wholesalePrice = null) =>
        new { skuCode, attributes, retailPrice, wholesalePrice };

    public static Task<HttpResponseMessage> PostProductAsync(
        this HttpClient client, string name, Guid categoryId, bool hasVariants, params object[] skus) =>
        client.PostAsJsonAsync("/products", new { name, categoryId, hasVariants, skus });

    // Tạo sản phẩm trong một danh mục mới của tenant.
    public static async Task<ProductDto> CreateProductAsync(this HttpClient client, string name, bool hasVariants, params object[] skus)
    {
        var category = await client.CreateCategoryAsync($"Danh mục {Guid.NewGuid():N}");
        var response = await client.PostProductAsync(name, category.Id, hasVariants, skus);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    // Sản phẩm đơn với một SKU; trả về SKU đó.
    public static async Task<SkuDto> CreateSkuAsync(this HttpClient client, string skuCode) =>
        Assert.Single((await client.CreateProductAsync($"Sản phẩm {skuCode}", hasVariants: false, Sku(skuCode))).Skus);

    public static async Task<ProductDto> GetProductAsync(this HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ProductDto>($"/products/{id}"))!;

    public static async Task<PagedResult<ProductListItemDto>> ListProductsAsync(this HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<PagedResult<ProductListItemDto>>($"/products{query}"))!;

    public static Task<HttpResponseMessage> PostBarcodeAsync(this HttpClient client, Guid skuId, string symbology, string? code = null) =>
        client.PostAsJsonAsync($"/skus/{skuId}/barcodes", new { symbology, code });

    public static async Task<BarcodeDto> AddBarcodeAsync(this HttpClient client, Guid skuId, string symbology, string? code = null)
    {
        var response = await client.PostBarcodeAsync(skuId, symbology, code);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BarcodeDto>())!;
    }

    // Các SkuUpserted tenant đã ghi vào outbox, theo SKU rồi theo version.
    public static async Task<IReadOnlyList<SkuUpserted>> SkuEventsAsync(this ApiFactory factory, Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var payloads = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Set<OutboxMessage>()
            .Where(message => message.Type == nameof(SkuUpserted))
            .Select(message => message.Payload)
            .ToListAsync();
        return payloads
            .Select(payload => JsonSerializer.Deserialize<SkuUpserted>(payload, EventEnvelope.JsonOptions)!)
            .OrderBy(message => message.SkuCode, StringComparer.Ordinal).ThenBy(message => message.Version)
            .ToList();
    }

    // Kiểm mã HTTP và trường `code` của ProblemDetails (docs/design/api/README.md).
    public static async Task AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
