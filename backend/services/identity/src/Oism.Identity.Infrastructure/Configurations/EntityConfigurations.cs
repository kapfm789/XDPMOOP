using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Identity.Domain;

namespace Oism.Identity.Infrastructure.Configurations;

// Bảng và ràng buộc: docs/design/data-model/identity.md.
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder) =>
        builder.Property(tenant => tenant.Status).HasConversion<string>();
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint("ck_users_email_or_phone", "email IS NOT NULL OR phone IS NOT NULL"));
        builder.Property(user => user.Role).HasConversion<string>();
        builder.HasOne<Tenant>().WithMany().HasForeignKey(user => user.TenantId).OnDelete(DeleteBehavior.Restrict);

        // Unique trên toàn hệ thống, không theo tenant: lúc đăng nhập chưa biết tenant (ADR-0011).
        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasIndex(user => user.Phone).IsUnique();
        builder.HasIndex(user => new { user.TenantId, user.CreatedAt });
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Ignore(token => token.WasReplaced);
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.TenantId, token.UserId });
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.Property(branch => branch.Type).HasConversion<string>();
        builder.HasIndex(branch => new { branch.TenantId, branch.Code }).IsUnique();
    }
}
