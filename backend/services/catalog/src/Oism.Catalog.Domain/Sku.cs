using Oism.SharedKernel;

namespace Oism.Catalog.Domain;

public sealed class Sku : ITenantOwned
{
    private readonly List<Barcode> _barcodes = [];

    private Sku()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProductId { get; private set; }

    public string SkuCode { get; private set; } = null!;

    // Ví dụ color: Đen, size: M; rỗng với sản phẩm đơn.
    public IReadOnlyDictionary<string, string> Attributes { get; private set; } = null!;

    public decimal RetailPrice { get; private set; }

    public decimal WholesalePrice { get; private set; }

    public bool IsActive { get; private set; }

    // Tăng 1 mỗi lần SKU, mã vạch hoặc giá của nó đổi; đi kèm SkuUpserted để bên nhận bỏ qua bản cũ.
    public long Version { get; private set; }

    public IReadOnlyCollection<Barcode> Barcodes => _barcodes;

    // SKU chỉ sinh ra qua Product.AddSku.
    internal static Sku Create(
        Guid productId, string skuCode, IReadOnlyDictionary<string, string> attributes, decimal retailPrice, decimal wholesalePrice) => new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            SkuCode = skuCode.Trim(),
            Attributes = attributes,
            RetailPrice = retailPrice,
            WholesalePrice = wholesalePrice,
            IsActive = true,
            Version = 1,
        };

    public void Update(IReadOnlyDictionary<string, string> attributes, bool isActive)
    {
        Attributes = attributes;
        IsActive = isActive;
        Version++;
    }

    public void SetPrices(decimal retailPrice, decimal wholesalePrice)
    {
        RetailPrice = retailPrice;
        WholesalePrice = wholesalePrice;
        Version++;
    }

    public Barcode AddBarcode(string code, BarcodeSymbology symbology)
    {
        var barcode = new Barcode(Id, code, symbology);
        _barcodes.Add(barcode);
        Version++;
        return barcode;
    }

    // False khi SKU không có mã vạch đó.
    public bool RemoveBarcode(Guid barcodeId)
    {
        if (_barcodes.RemoveAll(barcode => barcode.Id == barcodeId) == 0)
            return false;

        Version++;
        return true;
    }

    // Tên hoặc trạng thái của sản phẩm đổi thì trạng thái SKU gửi sang service khác cũng đổi.
    internal void Touch() => Version++;
}
