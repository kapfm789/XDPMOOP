using System.Net;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Domain;

namespace Oism.Catalog.IntegrationTests.Products;

// UC-PROD-03: mã vạch.
public sealed class BarcodeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    // Điều kiện xong của W1-07: EAN-13 đúng số kiểm tra.
    [Fact]
    [Trait("UseCase", "UC-PROD-03 AC-1")]
    public async Task AddBarcode_Ean13WithoutCode_GeneratesInternalCodesInSequencePerTenant()
    {
        var sku = await Staff.CreateSkuAsync("BUT-01");
        var otherSku = await Staff.CreateSkuAsync("BUT-02");
        var otherTenant = factory.CreateClient(Roles.Staff, Guid.NewGuid());

        var first = await Staff.AddBarcodeAsync(sku.Id, "EAN13");
        var second = await Staff.AddBarcodeAsync(otherSku.Id, "EAN13", code: " ");
        var firstElsewhere = await otherTenant.AddBarcodeAsync((await otherTenant.CreateSkuAsync("BUT-01")).Id, "EAN13");

        // Tiền tố 200, 9 chữ số tăng dần theo tenant, số kiểm tra.
        Assert.Equal(("2000000000015", "EAN13"), (first.Code, first.Symbology));
        Assert.Equal("2000000000022", second.Code);
        Assert.Equal("2000000000015", firstElsewhere.Code);
        Assert.All([first, second], barcode => Assert.True(Ean13.IsValid(barcode.Code)));
        Assert.Equal(first, Assert.Single(Assert.Single((await Staff.GetProductAsync(sku.ProductId)).Skus).Barcodes));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-03 AC-2")]
    public async Task AddBarcode_ManualEan13_AcceptsOnlyACorrectCheckDigit()
    {
        var sku = await Staff.CreateSkuAsync("BUT-01");

        var wrongCheckDigit = await Staff.PostBarcodeAsync(sku.Id, "EAN13", "8934567890127");
        var tooShort = await Staff.PostBarcodeAsync(sku.Id, "EAN13", "893456789012");
        var notDigits = await Staff.PostBarcodeAsync(sku.Id, "EAN13", "89345678901AB");
        var valid = await Staff.AddBarcodeAsync(sku.Id, "EAN13", " 8934567890120 ");

        await wrongCheckDigit.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await tooShort.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await notDigits.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal("8934567890120", valid.Code);
        Assert.Equal([valid], Assert.Single((await Staff.GetProductAsync(sku.ProductId)).Skus).Barcodes);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-03 AC-3")]
    public async Task AddBarcode_ManualCode128_AcceptsLettersAndDigitsUpTo48Characters()
    {
        var sku = await Staff.CreateSkuAsync("BUT-01");
        var longest = new string('A', 24) + new string('7', 24);

        var valid = await Staff.AddBarcodeAsync(sku.Id, "Code128", longest);
        var tooLong = await Staff.PostBarcodeAsync(sku.Id, "Code128", longest + "X");
        var withSymbol = await Staff.PostBarcodeAsync(sku.Id, "Code128", "ABC-123");
        var missingCode = await Staff.PostBarcodeAsync(sku.Id, "Code128");
        var unknownSymbology = await Staff.PostBarcodeAsync(sku.Id, "QR", "ABC123");

        Assert.Equal((longest, "Code128"), (valid.Code, valid.Symbology));
        await tooLong.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await withSymbol.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await missingCode.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await unknownSymbology.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-03 AC-4")]
    [Trait("UseCase", "UC-PROD-03 AC-5")]
    public async Task AddBarcode_CodeAlreadyInTenant_Returns409ButAnotherTenantMayUseIt()
    {
        var sku = await Staff.CreateSkuAsync("BUT-01");
        var otherSku = await Staff.CreateSkuAsync("BUT-02");
        var otherTenant = factory.CreateClient(Roles.Staff, Guid.NewGuid());
        var barcode = await Staff.AddBarcodeAsync(sku.Id, "Code128", "ABC123");

        var onAnotherSku = await Staff.PostBarcodeAsync(otherSku.Id, "Code128", "ABC123");
        var onTheSameSku = await Staff.PostBarcodeAsync(sku.Id, "Code128", "ABC123");
        var elsewhere = await otherTenant.AddBarcodeAsync((await otherTenant.CreateSkuAsync("BUT-01")).Id, "Code128", "ABC123");

        await onAnotherSku.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        await onTheSameSku.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        Assert.Equal("ABC123", elsewhere.Code);
        // Một mã vạch ra đúng một SKU.
        Assert.Equal([barcode], Assert.Single((await Staff.GetProductAsync(sku.ProductId)).Skus).Barcodes);
        Assert.Empty(Assert.Single((await Staff.GetProductAsync(otherSku.ProductId)).Skus).Barcodes);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-03 AC-1")]
    public async Task RemoveBarcode_ExistingThenUnknown_Returns204Then404()
    {
        var sku = await Staff.CreateSkuAsync("BUT-01");
        var otherSku = await Staff.CreateSkuAsync("BUT-02");
        var barcode = await Staff.AddBarcodeAsync(sku.Id, "EAN13");

        // Mã vạch phải được xóa qua đúng SKU của nó.
        var viaAnotherSku = await Staff.DeleteAsync($"/skus/{otherSku.Id}/barcodes/{barcode.Id}");
        var removed = await Staff.DeleteAsync($"/skus/{sku.Id}/barcodes/{barcode.Id}");
        var again = await Staff.DeleteAsync($"/skus/{sku.Id}/barcodes/{barcode.Id}");
        var unknownSku = await Staff.PostBarcodeAsync(Guid.NewGuid(), "EAN13");

        await viaAnotherSku.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await again.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await unknownSku.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Empty(Assert.Single((await Staff.GetProductAsync(sku.ProductId)).Skus).Barcodes);
    }
}
