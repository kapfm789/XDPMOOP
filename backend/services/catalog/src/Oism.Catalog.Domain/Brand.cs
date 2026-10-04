using Oism.SharedKernel;

namespace Oism.Catalog.Domain;

public sealed class Brand(string name) : ITenantOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = name;

    public void Rename(string name) => Name = name;
}
