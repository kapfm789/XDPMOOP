namespace Oism.Core.Application;

// Một use case là một transaction: handler mở ở đầu và commit ở cuối (docs/conventions/backend.md).
public interface IUnitOfWork
{
    Task<ITransaction> BeginAsync(CancellationToken ct);
}

public interface ITransaction : IAsyncDisposable
{
    // Đưa các thay đổi đang chờ xuống database mà chưa commit, để chỉ mục unique lên tiếng và dòng vừa chèn
    // được giữ khóa trước các bước sau. Vi phạm chỉ mục unique ném DuplicateException.
    Task SaveAsync(CancellationToken ct);

    // Lưu mọi thay đổi rồi commit. Bị hủy mà chưa commit thì rollback.
    // Vi phạm chỉ mục unique ném DuplicateException.
    Task CommitAsync(CancellationToken ct);
}

public interface IEventPublisher
{
    // Ghi thông điệp vào outbox trong transaction của use case; không gửi thẳng lên RabbitMQ
    // (docs/architecture/messaging.md mục "Phía phát").
    void Enqueue<TPayload>(TPayload payload)
        where TPayload : notnull;
}

// Thời hạn giữ hàng, lấy từ cấu hình `Reservation:HoldMinutes` (docs/design/flows/reserve-stock.md).
public sealed record ReservationSettings(int HoldMinutes);

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

// Danh sách có phân trang: docs/design/api/README.md mục "Dữ liệu".
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
