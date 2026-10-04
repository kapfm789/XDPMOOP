namespace Oism.BuildingBlocks.Messaging;

// Consumer base. Trước khi gọi HandleAsync, EventConsumerHost đã đặt tenant context theo thông điệp, mở transaction
// và ghi inbox; sau khi HandleAsync xong nó lưu thay đổi và commit. Vì vậy consumer và handler nó gọi
// không tự mở transaction và không tự commit.
public abstract class EventConsumer<TPayload>
{
    public abstract Task HandleAsync(TPayload payload, CancellationToken ct);
}

internal sealed record ConsumerRegistration(
    string MessageType, Func<IServiceProvider, EventEnvelope, CancellationToken, Task> HandleAsync);
