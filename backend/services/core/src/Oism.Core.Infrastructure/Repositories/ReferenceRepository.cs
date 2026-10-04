using Microsoft.EntityFrameworkCore;
using Oism.Core.Application.References;
using Oism.Core.Domain.References;

namespace Oism.Core.Infrastructure.Repositories;

internal sealed class ReferenceRepository(CoreDbContext db) : IReferenceRepository
{
    public Task<SkuRef?> FindSkuAsync(Guid skuId, CancellationToken ct) =>
        db.SkuRefs.SingleOrDefaultAsync(sku => sku.SkuId == skuId, ct);

    public Task<BranchRef?> FindBranchAsync(Guid branchId, CancellationToken ct) =>
        db.BranchRefs.SingleOrDefaultAsync(branch => branch.BranchId == branchId, ct);

    public async Task<IReadOnlyList<SkuRef>> ListSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct) =>
        await db.SkuRefs.AsNoTracking().Where(sku => skuIds.Contains(sku.SkuId)).ToListAsync(ct);

    public void Add(SkuRef sku) => db.Add(sku);

    public void Add(BranchRef branch) => db.Add(branch);
}
