using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Identity.Application.Tenants;
using Oism.Identity.Domain;

namespace Oism.Identity.Infrastructure.Repositories;

internal sealed class TenantRepository(IdentityDbContext db, ITenantContext tenantContext) : ITenantRepository
{
    public void Add(Tenant tenant)
    {
        db.Add(tenant);
        tenantContext.Set(tenant.Id);
    }

    public Task<string?> GetNameAsync(Guid id, CancellationToken ct) =>
        db.Tenants.Where(tenant => tenant.Id == id).Select(tenant => tenant.Name).SingleOrDefaultAsync(ct);
}
