using Oism.SharedKernel;

namespace Oism.Catalog.Domain;

public sealed class Product : ITenantOwned
{
    private readonly List<Sku> _skus = [];

    private Product()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid? BrandId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    // False thì sản phẩm có đúng một SKU (UC-PROD-02 AC-1).
    public bool HasVariants { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Sku> Skus => _skus;

    public bool CanAddSku => HasVariants || _skus.Count == 0;

    public static Product Create(string name, Guid categoryId, Guid? brandId, string? description, bool hasVariants) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        CategoryId = categoryId,
        BrandId = brandId,
        Description = NormalizeDescription(description),
        HasVariants = hasVariants,
        IsActive = true,
    };

    public Sku AddSku(
        string skuCode, IReadOnlyDictionary<string, string> attributes, decimal retailPrice, decimal wholesalePrice)
    {
        if (!CanAddSku)
            throw new InvalidOperationException("A product without variants has exactly one SKU.");

        var sku = Sku.Create(Id, skuCode, attributes, retailPrice, wholesalePrice);
        _skus.Add(sku);
        return sku;
    }

    // True khi tên hoặc trạng thái đổi: tên hiển thị và trạng thái bán của mọi SKU đổi theo,
    // nên mọi SKU tăng version và phải phát lại SkuUpserted.
    public bool Update(string name, Guid categoryId, Guid? brandId, string? description, bool isActive)
    {
        name = name.Trim();
        var skusChanged = name != Name || isActive != IsActive;

        Name = name;
        CategoryId = categoryId;
        BrandId = brandId;
        Description = NormalizeDescription(description);
        IsActive = isActive;

        if (skusChanged)
            _skus.ForEach(sku => sku.Touch());
        return skusChanged;
    }

    // Tên sản phẩm kèm các giá trị thuộc tính, ví dụ "Áo thun A, Đen, M". jsonb không giữ thứ tự khóa,
    // nên thuộc tính xếp theo tên để tên hiển thị không đổi giữa các lần đọc.
    public string DisplayNameOf(Sku sku) => string.Join(
        ", ", sku.Attributes.OrderBy(attribute => attribute.Key, StringComparer.Ordinal).Select(attribute => attribute.Value).Prepend(Name));

    // Ngừng bán sản phẩm là ngừng bán mọi SKU của nó.
    public bool IsSellable(Sku sku) => IsActive && sku.IsActive;

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
