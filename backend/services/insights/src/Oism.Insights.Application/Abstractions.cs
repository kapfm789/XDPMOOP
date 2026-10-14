namespace Oism.Insights.Application;

// Một use case là một transaction: handler mở ở đầu và commit ở cuối (docs/conventions/backend.md).
// Handler gọi từ consumer thì không dùng: consumer base đã mở transaction và sẽ commit.
public interface IUnitOfWork
{
    Task<ITransaction> BeginAsync(CancellationToken ct);
}

public interface ITransaction : IAsyncDisposable
{
    // Lưu mọi thay đổi rồi commit. Bị hủy mà chưa commit thì rollback.
    Task CommitAsync(CancellationToken ct);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
