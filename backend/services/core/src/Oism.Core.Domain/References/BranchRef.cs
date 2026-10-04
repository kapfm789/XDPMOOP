using Oism.SharedKernel;

namespace Oism.Core.Domain.References;

// Bản sao chi nhánh của `identity`, dựng từ BranchUpserted. Chỉ consumer ghi vào bảng này (docs/design/data-model/core.md).
public sealed class BranchRef(Guid branchId) : ITenantOwned
{
    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; } = branchId;

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    // `Store` hoặc `Warehouse`.
    public string Type { get; private set; } = null!;

    public bool IsActive { get; private set; }

    // 0 khi chưa nhận thông điệp nào; version của `identity` bắt đầu từ 1.
    public long Version { get; private set; }

    // Thông điệp trạng thái có thể tới trùng hoặc sai thứ tự: chỉ ghi đè khi version lớn hơn bản đang giữ.
    // False khi thông điệp bị bỏ qua.
    public bool Apply(string code, string name, string type, bool isActive, long version)
    {
        if (version <= Version)
            return false;

        Code = code;
        Name = name;
        Type = type;
        IsActive = isActive;
        Version = version;
        return true;
    }

    // UC-ORD-02 AC-4: chi nhánh đã tắt không nhận đơn mới.
    public void EnsureActive()
    {
        if (!IsActive)
            throw new InactiveReferenceException("InactiveBranch", BranchId);
    }
}
