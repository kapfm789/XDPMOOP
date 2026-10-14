using Oism.Contracts;

namespace Oism.Insights.Application.Notifications.NotifyOrderCreated;

// UC-SIM-02 AC-1: đơn vừa được giữ hàng thì đẩy OrderCreated tới admin và POS của đúng tenant và chi nhánh.
public sealed class NotifyOrderCreatedHandler(INotificationPublisher notifications)
{
    public Task Handle(OrderReserved message, CancellationToken ct)
    {
        notifications.OrderCreated(new OrderCreatedNotification(
            message.OrderId, message.OrderNumber, message.Channel, message.BranchId, message.TotalAmount));
        return Task.CompletedTask;
    }
}
