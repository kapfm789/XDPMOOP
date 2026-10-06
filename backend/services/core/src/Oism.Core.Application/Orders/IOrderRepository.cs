using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Orders;

public interface IOrderRepository
{
    // Khóa dòng đơn bằng SELECT ... FOR UPDATE rồi nạp các dòng của nó. Phải gọi trong transaction của use case,
    // trước mọi kiểm tra trạng thái và trước khi khóa số dư. Null khi đơn không có trong tenant.
    Task<Order?> GetForUpdateAsync(Guid id, CancellationToken ct);

    // Đơn POS đã tạo với khóa này, kèm dòng đơn và thanh toán. Chỉ để đọc; null khi chưa có.
    Task<Order?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);

    void Add(Order order);
}
