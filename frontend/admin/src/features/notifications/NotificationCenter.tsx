import { formatMoney, playChime, useNotifications } from '@oism/shared';
import { useQueryClient } from '@tanstack/react-query';
import { Badge, Button, Dropdown, Empty, notification, Typography } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Icon } from '../common/icons';
import { CHANNEL_LABELS } from '../orders/labels';

type Received = { key: string; title: string; description: string; path: string };

// Chuông chỉ giữ các thông báo nhận được trong phiên đang mở; không có API nào lưu thông báo.
const KEEP = 10;

// UC-SIM-02, UC-REP-03: OrderCreated và LowStock hiện popup kèm âm thanh ở mọi màn hình; bấm vào mở chi tiết đơn hoặc
// màn hình tồn (docs/design/ui/admin.md). Danh sách đang mở tự tải lại nên đơn mới hiện mà không cần tải lại trang.
// Chuông ở thanh trên đếm thông báo chưa xem và liệt kê các thông báo gần nhất.
export function NotificationCenter() {
  // Giữ lâu hơn mặc định 4,5 giây để kịp bấm vào.
  const [api, holder] = notification.useNotification({ placement: 'bottomRight', duration: 10 });
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [received, setReceived] = useState<Received[]>([]);
  const [unread, setUnread] = useState(0);

  const show = (item: Received, kind: 'info' | 'warning') => {
    playChime();
    setReceived((current) => [item, ...current.filter((other) => other.key !== item.key)].slice(0, KEEP));
    setUnread((count) => count + 1);
    api[kind]({
      key: item.key,
      title: item.title,
      description: item.description,
      style: { cursor: 'pointer' },
      onClick: () => {
        api.destroy(item.key);
        navigate(item.path);
      },
    });
  };

  useNotifications({
    onOrderCreated: (order) => {
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
      show(
        {
          key: `order-${order.orderId}`,
          title: `Đơn mới ${order.orderNumber}`,
          description: `${CHANNEL_LABELS[order.channel] ?? order.channel} · ${formatMoney(order.totalAmount)}`,
          path: `/orders/${order.orderId}`,
        },
        'info',
      );
    },
    onLowStock: (alert) => {
      // Màn hình tồn và danh sách cảnh báo ở trang tổng quan tải lại.
      void queryClient.invalidateQueries({ queryKey: ['stock'] });
      void queryClient.invalidateQueries({ queryKey: ['low-stock'] });
      show(
        {
          key: `low-stock-${alert.branchId}-${alert.skuId}`,
          title: `Tồn thấp: ${alert.skuCode ?? alert.skuId}`,
          description: `${alert.name ? `${alert.name} · ` : ''}còn ${alert.available}, ngưỡng ${alert.threshold}`,
          path: '/inventory/stock',
        },
        'warning',
      );
    },
  });

  return (
    <>
      {holder}
      <Dropdown
        trigger={['click']}
        onOpenChange={(open) => open && setUnread(0)}
        popupRender={() => (
          <div
            style={{
              width: 320,
              background: '#fff',
              borderRadius: 7,
              boxShadow: '0 6px 16px rgba(23, 32, 51, 0.12)',
              padding: 8,
            }}
          >
            <Typography.Text strong style={{ display: 'block', padding: '4px 8px 8px' }}>
              Thông báo
            </Typography.Text>
            {received.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có thông báo nào trong phiên này" />
            ) : (
              received.map((item) => (
                <Button
                  key={item.key}
                  type="text"
                  block
                  onClick={() => navigate(item.path)}
                  style={{ height: 'auto', padding: '8px', textAlign: 'left', display: 'block', whiteSpace: 'normal' }}
                >
                  <Typography.Text strong style={{ display: 'block' }}>
                    {item.title}
                  </Typography.Text>
                  <Typography.Text type="secondary">{item.description}</Typography.Text>
                </Button>
              ))
            )}
          </div>
        )}
      >
        <Badge count={unread} size="small" offset={[-4, 4]}>
          <Button type="text" aria-label="Thông báo" icon={<Icon name="bell" />} />
        </Badge>
      </Dropdown>
    </>
  );
}
