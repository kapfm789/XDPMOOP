using Oism.SharedKernel;

namespace Oism.Core.Domain.Inventory;

// Bảng: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ".
public sealed class Supplier : ITenantOwned
{
    private Supplier()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; }

    public static Supplier Create(string name, string? phone) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
        IsActive = true,
    };
}
