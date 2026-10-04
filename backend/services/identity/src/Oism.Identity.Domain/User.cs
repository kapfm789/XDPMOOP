using Oism.SharedKernel;

namespace Oism.Identity.Domain;

// Tên vai trò đi vào claim `role` của JWT; phải khớp Roles ở Oism.BuildingBlocks/Auth.
public enum UserRole
{
    Owner,
    Staff,
    Cashier,
}

public sealed class User : ITenantOwned
{
    private User()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string FullName { get; private set; } = null!;

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public Guid? BranchId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(
        string fullName, string? email, string? phone, string passwordHash, UserRole role, Guid? branchId, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = NormalizeEmail(email),
            Phone = NormalizePhone(phone),
            PasswordHash = passwordHash,
            Role = role,
            BranchId = branchId,
            IsActive = true,
            CreatedAt = now,
        };

    public void Update(string fullName, UserRole role, Guid? branchId, bool isActive)
    {
        FullName = fullName.Trim();
        Role = role;
        BranchId = branchId;
        IsActive = isActive;
    }

    // Email so khớp không phân biệt hoa thường nên lưu chữ thường. Chuỗi trống coi như không có.
    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    public static string? NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}
