namespace Oism.Insights.Application.Notifications;

// Thông báo SignalR: tên và trường ở docs/design/events.md mục "Thông báo SignalR".
public sealed record OrderCreatedNotification(Guid OrderId, string OrderNumber, string Channel, Guid BranchId, decimal TotalAmount);

public sealed record LowStockNotification(Guid BranchId, Guid SkuId, string? SkuCode, string? Name, int Available, int Threshold);

// Cài đặt ở Api. Thông báo được giữ lại và chỉ gửi sau khi transaction của consumer commit; rollback thì không gửi.
// Group lấy theo tenant context hiện tại, không bao giờ gửi cho mọi kết nối.
public interface INotificationPublisher
{
    // Tới group của tenant và group của chi nhánh đặt đơn.
    void OrderCreated(OrderCreatedNotification notification);

    // Tới group của tenant.
    void LowStock(LowStockNotification notification);
}
