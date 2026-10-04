namespace Oism.Catalog.Application;

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
