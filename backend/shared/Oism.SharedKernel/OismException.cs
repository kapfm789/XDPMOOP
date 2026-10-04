namespace Oism.SharedKernel;

// Lỗi nghiệp vụ có mã. Middleware ở Oism.BuildingBlocks/Web đổi sang ProblemDetails;
// bảng mã và HTTP status: docs/conventions/backend.md mục "Lỗi".
public abstract class OismException(string code, string message, object? details = null) : Exception(message)
{
    public string Code { get; } = code;

    public object? Details { get; } = details;
}

public sealed class NotFoundException(string resource)
    : OismException("not_found", $"Không tìm thấy {resource}");

public sealed class DuplicateException(string message)
    : OismException("duplicate", message);

// Sai thông tin đăng nhập hoặc refresh token không dùng được. Một thông báo chung cho mọi trường hợp,
// để phản hồi không lộ tài khoản có tồn tại hay không.
public sealed class UnauthenticatedException()
    : OismException("unauthenticated", "Thông tin đăng nhập không đúng hoặc phiên đã hết hạn");
