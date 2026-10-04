using Oism.Core.Domain.References;

namespace Oism.Core.Application.References;

// Bản sao SKU và chi nhánh của tenant hiện tại.
public interface IReferenceRepository
{
    Task<SkuRef?> FindSkuAsync(Guid skuId, CancellationToken ct);

    Task<BranchRef?> FindBranchAsync(Guid branchId, CancellationToken ct);

    // Chỉ để đọc; SKU không có bản sao thì vắng trong kết quả.
    Task<IReadOnlyList<SkuRef>> ListSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct);

    // Đơn từ sàn mang mã SKU chứ không mang id. Chỉ để đọc; mã không có bản sao thì vắng trong kết quả.
    Task<IReadOnlyList<SkuRef>> ListSkusByCodeAsync(IReadOnlyCollection<string> skuCodes, CancellationToken ct);

    void Add(SkuRef sku);

    void Add(BranchRef branch);
}
