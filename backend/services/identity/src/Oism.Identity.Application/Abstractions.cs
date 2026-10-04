using Oism.Identity.Domain;

namespace Oism.Identity.Application;

// Một use case là một transaction: handler mở ở đầu và commit ở cuối (docs/conventions/backend.md).
public interface IUnitOfWork
{
    Task<ITransaction> BeginAsync(CancellationToken ct);
}

public interface ITransaction : IAsyncDisposable
{
    // Lưu mọi thay đổi rồi commit. Bị hủy mà chưa commit thì rollback.
    // Vi phạm chỉ mục unique ném DuplicateException.
    Task CommitAsync(CancellationToken ct);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

public sealed record AccessToken(string Value, int ExpiresIn);

public interface IAccessTokenIssuer
{
    // JWT ký RS256 mang sub, tenant_id, role và branch_id nếu có (docs/architecture/security.md).
    AccessToken Issue(User user, DateTimeOffset now);
}

// Danh sách có phân trang: docs/design/api/README.md mục "Dữ liệu".
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
