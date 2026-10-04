using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.Catalog.Application.Brands;
using Oism.Catalog.Application.Categories;

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

    // Kiểm mã HTTP và trường `code` của ProblemDetails (docs/design/api/README.md).
    public static async Task AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
