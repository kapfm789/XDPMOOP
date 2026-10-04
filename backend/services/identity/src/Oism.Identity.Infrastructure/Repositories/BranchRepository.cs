using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Identity.Application.Branches;
using Oism.Identity.Domain;

namespace Oism.Identity.Infrastructure.Repositories;

internal sealed class BranchRepository(IdentityDbContext db, ITenantContext tenant) : IBranchRepository
{
    public async Task<IReadOnlyList<BranchDto>> ListAsync(bool? isActive, CancellationToken ct) =>
        await db.Branches.AsNoTracking()
            .Where(branch => isActive == null || branch.IsActive == isActive)
            .OrderBy(branch => branch.Code)
            .Select(branch => new BranchDto(
                branch.Id, branch.Code, branch.Name, branch.Type.ToString(), branch.Address, branch.IsActive))
            .ToListAsync(ct);

    // Điều kiện tenant_id nằm ngay trong câu khóa để chỉ khóa dòng của tenant hiện tại;
    // Global Query Filter vẫn được áp ở lớp ngoài.
    public async Task<Branch?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        (await db.Branches
            .FromSql($"SELECT * FROM identity.branches WHERE tenant_id = {tenant.TenantId} AND id = {id} FOR UPDATE")
            .ToListAsync(ct))
        .SingleOrDefault();

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => db.Branches.AnyAsync(branch => branch.Id == id, ct);

    public void Add(Branch branch) => db.Add(branch);
}
