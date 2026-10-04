using Oism.SharedKernel;

namespace Oism.Catalog.Domain;

// Tên thành viên là giá trị lưu ở cột symbology và nhận từ API: docs/design/data-model/catalog.md.
public enum BarcodeSymbology
{
    EAN13,
    Code128,
}

public sealed class Barcode : ITenantOwned
{
    private Barcode()
    {
    }

    internal Barcode(Guid skuId, string code, BarcodeSymbology symbology)
    {
        Id = Guid.NewGuid();
        SkuId = skuId;
        Code = code;
        Symbology = symbology;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SkuId { get; private set; }

    public string Code { get; private set; } = null!;

    public BarcodeSymbology Symbology { get; private set; }
}
