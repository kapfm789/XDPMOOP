using Oism.SharedKernel;

namespace Oism.Identity.Domain;

// Tên loại đi vào `type` của BranchUpserted: docs/design/events.md.
public enum BranchType
{
    Store,
    Warehouse,
}

public sealed class Branch : ITenantOwned
{
    private Branch()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public BranchType Type { get; private set; }

    public string? Address { get; private set; }

    public bool IsActive { get; private set; }

    // Tăng 1 mỗi lần sửa; đi kèm BranchUpserted để bên nhận bỏ qua bản cũ.
    public long Version { get; private set; }

    // Chi nhánh mới luôn ở trạng thái đang hoạt động (UC-AUTH-04 AC-1).
    public static Branch Create(string code, string name, BranchType type, string? address) => new()
    {
        Id = Guid.NewGuid(),
        Code = code.Trim(),
        Name = name.Trim(),
        Type = type,
        Address = NormalizeAddress(address),
        IsActive = true,
        Version = 1,
    };

    public void Update(string name, BranchType type, string? address)
    {
        Name = name.Trim();
        Type = type;
        Address = NormalizeAddress(address);
        Version++;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Version++;
    }

    private static string? NormalizeAddress(string? address) =>
        string.IsNullOrWhiteSpace(address) ? null : address.Trim();
}
