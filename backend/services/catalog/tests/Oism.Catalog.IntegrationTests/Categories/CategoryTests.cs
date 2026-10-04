using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Application.Categories;

namespace Oism.Catalog.IntegrationTests.Categories;

// UC-PROD-01: danh mục phân cấp (điều kiện xong của W1-06: CRUD và test cây danh mục).
public sealed class CategoryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _staff = factory.CreateClient(Roles.Staff, Guid.NewGuid());

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-1")]
    public async Task UpsertCategory_ChildOfChild_TreeGainsOneLevelEachTime()
    {
        var root = await _staff.CreateCategoryAsync("Thời trang");
        var child = await _staff.CreateCategoryAsync("Áo", root.Id);
        var grandchild = await _staff.CreateCategoryAsync("Áo thun", child.Id);
        await _staff.CreateCategoryAsync("Điện tử");

        var tree = await _staff.GetCategoryTreeAsync();

        Assert.Equal(["Điện tử", "Thời trang"], tree.Select(node => node.Name).Order());
        var rootNode = tree.Single(node => node.Id == root.Id);
        var childNode = Assert.Single(rootNode.Children);
        Assert.Equal(child.Id, childNode.Id);
        var grandchildNode = Assert.Single(childNode.Children);
        Assert.Equal(grandchild.Id, grandchildNode.Id);
        Assert.Equal(child.Id, grandchildNode.ParentId);
        Assert.Empty(grandchildNode.Children);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-1")]
    public async Task UpsertCategory_MoveUnderAnotherBranch_SubtreeMovesWithIt()
    {
        var clothes = await _staff.CreateCategoryAsync("Quần áo");
        var shirts = await _staff.CreateCategoryAsync("Áo", clothes.Id);
        await _staff.CreateCategoryAsync("Áo sơ mi", shirts.Id);
        var sale = await _staff.CreateCategoryAsync("Khuyến mãi");

        var response = await _staff.PutAsJsonAsync($"/categories/{shirts.Id}", new { name = "Áo giảm giá", parentId = sale.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
        Assert.Equal("Áo giảm giá", updated.Name);
        Assert.Equal(sale.Id, updated.ParentId);
        var tree = await _staff.GetCategoryTreeAsync();
        Assert.Empty(tree.Single(node => node.Id == clothes.Id).Children);
        var moved = Assert.Single(tree.Single(node => node.Id == sale.Id).Children);
        Assert.Equal("Áo sơ mi", Assert.Single(moved.Children).Name);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-2")]
    public async Task UpsertCategory_ParentIsItself_Returns400()
    {
        var category = await _staff.CreateCategoryAsync("Giày");

        var response = await _staff.PutAsJsonAsync($"/categories/{category.Id}", new { name = "Giày", parentId = category.Id });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Null((await _staff.GetCategoryTreeAsync()).Single().ParentId);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-2")]
    public async Task UpsertCategory_ParentIsItsDescendant_Returns400()
    {
        var root = await _staff.CreateCategoryAsync("Gia dụng");
        var child = await _staff.CreateCategoryAsync("Bếp", root.Id);
        var grandchild = await _staff.CreateCategoryAsync("Nồi", child.Id);

        var response = await _staff.PutAsJsonAsync($"/categories/{root.Id}", new { name = "Gia dụng", parentId = grandchild.Id });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var rootNode = Assert.Single(await _staff.GetCategoryTreeAsync());
        Assert.Equal(root.Id, rootNode.Id);
        Assert.Equal(grandchild.Id, rootNode.Children.Single().Children.Single().Id);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-3")]
    public async Task DeleteCategory_HasChildCategory_Returns409AndKeepsBoth()
    {
        var root = await _staff.CreateCategoryAsync("Mỹ phẩm");
        await _staff.CreateCategoryAsync("Son", root.Id);

        var response = await _staff.DeleteAsync($"/categories/{root.Id}");

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "category_in_use");
        Assert.Single(Assert.Single(await _staff.GetCategoryTreeAsync()).Children);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-3")]
    public async Task DeleteCategory_Leaf_Returns204AndRemovesIt()
    {
        var root = await _staff.CreateCategoryAsync("Sách");
        var leaf = await _staff.CreateCategoryAsync("Truyện", root.Id);

        var response = await _staff.DeleteAsync($"/categories/{leaf.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(Assert.Single(await _staff.GetCategoryTreeAsync()).Children);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-1")]
    public async Task UpsertCategory_SameNameUnderSameParent_Returns409()
    {
        var root = await _staff.CreateCategoryAsync("Đồ chơi");
        await _staff.CreateCategoryAsync("Xếp hình", root.Id);

        var sameParent = await _staff.PostAsJsonAsync("/categories", new { name = "Xếp hình", parentId = root.Id });
        var sameRoot = await _staff.PostAsJsonAsync("/categories", new { name = "Đồ chơi" });
        var otherParent = await _staff.PostAsJsonAsync("/categories", new { name = "Xếp hình" });

        await sameParent.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        await sameRoot.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        Assert.Equal(HttpStatusCode.Created, otherParent.StatusCode);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-1")]
    public async Task UpsertCategory_UnknownIdOrParent_Returns404()
    {
        var update = await _staff.PutAsJsonAsync($"/categories/{Guid.NewGuid()}", new { name = "Không có" });
        var create = await _staff.PostAsJsonAsync("/categories", new { name = "Con", parentId = Guid.NewGuid() });
        var delete = await _staff.DeleteAsync($"/categories/{Guid.NewGuid()}");

        await update.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await create.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await delete.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("UseCase", "UC-PROD-01 AC-1")]
    public async Task UpsertCategory_BlankName_Returns400(string name)
    {
        var response = await _staff.PostAsJsonAsync("/categories", new { name });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-3")]
    public async Task Categories_Cashier_Returns403()
    {
        var cashier = factory.CreateClient(Roles.Cashier, Guid.NewGuid());

        var read = await cashier.GetAsync("/categories");
        var write = await cashier.PostAsJsonAsync("/categories", new { name = "Bị chặn" });

        await read.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await write.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }
}
