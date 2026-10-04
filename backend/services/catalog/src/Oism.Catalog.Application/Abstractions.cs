namespace Oism.Catalog.Application;

public interface IEventPublisher
{
    // Ghi thông điệp vào outbox trong transaction của use case; không gửi thẳng lên RabbitMQ
    // (docs/architecture/messaging.md mục "Phía phát").
    void Enqueue<TPayload>(TPayload payload)
        where TPayload : notnull;
}

// Danh sách có phân trang: docs/design/api/README.md mục "Dữ liệu".
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
