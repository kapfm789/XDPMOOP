using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Domain;

namespace Oism.Catalog.IntegrationTests.Concurrency;

public sealed class BarcodeConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Mã EAN-13 tự sinh lấy số kế tiếp của tenant: các request cùng lúc không được nhận cùng một số.
    [Fact]
    [Trait("UseCase", "UC-PROD-03 AC-1")]
    public async Task AddBarcode_TenGeneratedAtOnce_AllSucceedWithConsecutiveCodes()
    {
        var staff = factory.CreateClient(Roles.Staff, Guid.NewGuid());
        var skus = new List<Guid>();
        for (var i = 0; i < 10; i++)
            skus.Add((await staff.CreateSkuAsync($"SKU-{i:D2}")).Id);
        var gate = new TaskCompletionSource();

        var tasks = skus.Select(async skuId =>
        {
            await gate.Task;
            return await staff.AddBarcodeAsync(skuId, "EAN13");
        }).ToArray();
        gate.SetResult();
        var barcodes = await Task.WhenAll(tasks);

        Assert.Equal(Enumerable.Range(1, 10).Select(sequence => Ean13.Generate(sequence)), barcodes.Select(barcode => barcode.Code).Order());
    }
}
