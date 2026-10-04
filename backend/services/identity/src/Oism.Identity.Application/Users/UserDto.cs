using Oism.Identity.Domain;

namespace Oism.Identity.Application.Users;

// Không bao giờ mang mật khẩu hay giá trị băm (UC-AUTH-01 AC-4).
public sealed record UserDto(
    Guid Id, string FullName, string? Email, string? Phone, string Role, Guid? BranchId, bool IsActive, DateTimeOffset CreatedAt)
{
    public static UserDto From(User user) => new(
        user.Id, user.FullName, user.Email, user.Phone, user.Role.ToString(), user.BranchId, user.IsActive, user.CreatedAt);
}
