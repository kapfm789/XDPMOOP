using System.Text.RegularExpressions;
using FluentValidation;
using Oism.Identity.Domain;

namespace Oism.Identity.Application;

// Quy tắc đầu vào dùng chung cho đăng ký tenant và quản lý người dùng.
// Thông báo viết tiếng Việt vì giao diện hiện chúng cạnh từng ô nhập.
internal static partial class AccountRules
{
    public const int MinPasswordLength = 8;

    public static IRuleBuilderOptions<T, string> RequiredName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Không được để trống")
            .MaximumLength(200).WithMessage("Tối đa 200 ký tự");

    // Chuỗi trống coi như không nhập.
    public static IRuleBuilderOptions<T, string?> OptionalEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(email => string.IsNullOrWhiteSpace(email) || EmailPattern().IsMatch(email.Trim()))
            .WithMessage("Email không hợp lệ");

    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(phone => string.IsNullOrWhiteSpace(phone) || PhonePattern().IsMatch(phone.Trim()))
            .WithMessage("Số điện thoại gồm 8 đến 15 chữ số, có thể bắt đầu bằng dấu +");

    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Không được để trống")
            .MinimumLength(MinPasswordLength).WithMessage($"Mật khẩu tối thiểu {MinPasswordLength} ký tự")
            .MaximumLength(128).WithMessage("Mật khẩu tối đa 128 ký tự");

    // Owner chỉ sinh ra khi đăng ký tenant; API người dùng chỉ nhận Staff hoặc Cashier.
    public static IRuleBuilderOptions<T, string> AssignableRole<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(role => role is nameof(UserRole.Staff) or nameof(UserRole.Cashier))
            .WithMessage("Vai trò phải là Staff hoặc Cashier");

    public static bool HasContact(string? email, string? phone) =>
        !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(phone);

    public const string ContactRequired = "Cần ít nhất email hoặc số điện thoại";

    public const string CashierNeedsBranch = "Cashier phải gắn với một chi nhánh";

    // Một dấu @, có phần trước và sau, không khoảng trắng; email luôn có @ còn số điện thoại thì không,
    // nên một chuỗi không thể vừa là email của người này vừa là số điện thoại của người khác.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9]{8,15}$")]
    private static partial Regex PhonePattern();
}
