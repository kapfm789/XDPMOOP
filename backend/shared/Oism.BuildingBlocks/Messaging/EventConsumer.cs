namespace Oism.BuildingBlocks.Messaging;

// Consumer base. Trước khi gọi HandleAsync, EventConsumerHost đã đặt tenant context theo thông điệp, mở transaction
// và ghi inbox; sau khi HandleAsync xong nó lưu thay đổi và commit. Vì vậy consumer và handler nó gọi
// không tự mở transaction và không tự commit.
public abstract class EventConsumer<TPayload>
{
    public abstract Task HandleAsync(TPayload payload, CancellationToken ct);
}

// Việc chạy sau khi transaction của consumer đã commit, ví dụ đẩy thông báo SignalR: không gửi ra ngoài bên trong
// transaction. Đăng ký scoped; EventConsumerHost gọi mọi bản đăng ký của scope đó. Lỗi ở đây chỉ ghi log: thông điệp
// đã xử lý xong nên không thử lại.
public interface IAfterCommitAction
{
    Task RunAsync(CancellationToken ct);
}

internal sealed record ConsumerRegistration(
    string MessageType, Func<IServiceProvider, EventEnvelope, CancellationToken, Task> HandleAsync);
