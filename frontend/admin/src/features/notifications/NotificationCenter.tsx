import { formatMoney, playChime, useNotifications } from '@oism/shared';
import { useQueryClient } from '@tanstack/react-query';
import { notification } from 'antd';
import { useNavigate } from 'react-router-dom';
import { CHANNEL_LABELS } from '../orders/labels';

// UC-SIM-02, UC-REP-03: OrderCreated và LowStock hiện popup kèm âm thanh ở mọi màn hình; bấm vào mở chi tiết đơn hoặc
// màn hình tồn (docs/design/ui/admin.md). Danh sách đang mở tự tải lại nên đơn mới hiện mà không cần tải lại trang.
export function NotificationCenter() {
  // Giữ lâu hơn mặc định 4,5 giây để kịp bấm vào.
  const [api, holder] = notification.useNotification({ placement: 'bottomRight', duration: 10 });
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  useNotifications({
    onOrderCreated: (order) => {
      playChime();
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
      const key = `order-${order.orderId}`;
      api.info({
        key,
        title: `Đơn mới ${order.orderNumber}`,
        description: `${CHANNEL_LABELS[order.channel] ?? order.channel} · ${formatMoney(order.totalAmount)}`,
        style: { cursor: 'pointer' },
        onClick: () => {
          api.destroy(key);
          navigate(`/orders/${order.orderId}`);
        },
      });
    },
    onLowStock: (alert) => {
      playChime();
      // Màn hình tồn và danh sách cảnh báo ở trang tổng quan tải lại.
      void queryClient.invalidateQueries({ queryKey: ['stock'] });
      void queryClient.invalidateQueries({ queryKey: ['low-stock'] });
      const key = `low-stock-${alert.branchId}-${alert.skuId}`;
      api.warning({
        key,
        title: `Tồn thấp: ${alert.skuCode ?? alert.skuId}`,
        description: `${alert.name ? `${alert.name} · ` : ''}còn ${alert.available}, ngưỡng ${alert.threshold}`,
        style: { cursor: 'pointer' },
        onClick: () => {
          api.destroy(key);
          navigate('/inventory/stock');
        },
      });
    },
  });

  return holder;
}
