using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.SharedKernel;

namespace Oism.BuildingBlocks.Persistence;

// DbContext base của mọi service: schema riêng, Global Query Filter theo tenant, gán và kiểm TenantId khi ghi.
// Entity được nạp qua các lớp IEntityTypeConfiguration<T> trong assembly của DbContext con.
public abstract class OismDbContext(DbContextOptions options, ITenantContext tenant, string schema) : DbContext(options)
{
    // EF Core đọc lại thuộc tính này ở mỗi truy vấn, nên filter luôn theo tenant của scope hiện tại.
    // Chưa có tenant thì filter so với null và không trả dòng nào.
    private Guid? CurrentTenantId => tenant.TenantId;

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Select(t => t.ClrType).Where(IsTenantOwned).ToList())
            modelBuilder.Entity(entityType).HasQueryFilter(TenantFilter(entityType));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static bool IsTenantOwned(Type type) => typeof(ITenantOwned).IsAssignableFrom(type);

    // e => (Guid?)e.TenantId == this.CurrentTenantId
    private LambdaExpression TenantFilter(Type entityType)
    {
        var entity = Expression.Parameter(entityType, "e");
        var body = Expression.Equal(
            Expression.Convert(Expression.Property(entity, nameof(ITenantOwned.TenantId)), typeof(Guid?)),
            Expression.Property(Expression.Constant(this, typeof(OismDbContext)), nameof(CurrentTenantId)));
        return Expression.Lambda(body, entity);
    }

    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var tenantId = CurrentTenantId
                ?? throw new InvalidOperationException($"Cannot save {entry.Metadata.ClrType.Name} without a tenant context.");

            var property = entry.Property<Guid>(nameof(ITenantOwned.TenantId));
            if (entry.State == EntityState.Added && property.CurrentValue == Guid.Empty)
                property.CurrentValue = tenantId;
            else if (property.CurrentValue != tenantId)
                throw new InvalidOperationException($"{entry.Metadata.ClrType.Name} belongs to another tenant.");
        }
    }
}
