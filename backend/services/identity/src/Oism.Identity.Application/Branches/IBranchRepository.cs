using Oism.Identity.Domain;

namespace Oism.Identity.Application.Branches;

public interface IBranchRepository
{
    // Chi nhánh của tenant, xếp theo mã. isActive null là lấy tất cả.
    Task<IReadOnlyList<BranchDto>> ListAsync(bool? isActive, CancellationToken ct);

    // Khóa dòng chi nhánh: hai lần sửa đồng thời phải lần lượt để version tăng đúng từng bước.
    Task<Branch?> GetForUpdateAsync(Guid id, CancellationToken ct);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct);

    void Add(Branch branch);
}
