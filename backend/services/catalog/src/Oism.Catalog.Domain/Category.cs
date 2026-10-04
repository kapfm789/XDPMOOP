using Oism.SharedKernel;

namespace Oism.Catalog.Domain;

public sealed class Category(string name, Guid? parentId) : ITenantOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid TenantId { get; private set; }

    public Guid? ParentId { get; private set; } = parentId;

    public string Name { get; private set; } = name;

    public int SortOrder { get; private set; }

    // Đặt cha là chính nó hoặc con cháu của nó thì cây thành vòng (UC-PROD-01 AC-2).
    // parentOf: cha hiện tại của từng danh mục trong tenant.
    public bool CanMoveUnder(Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parentOf)
    {
        for (var ancestor = parentId; ancestor is { } id; ancestor = parentOf.GetValueOrDefault(id))
        {
            if (id == Id)
                return false;
        }

        return true;
    }

    public void Update(string name, Guid? parentId)
    {
        Name = name;
        ParentId = parentId;
    }
}
