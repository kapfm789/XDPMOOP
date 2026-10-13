import { Typography } from 'antd';
import { OrdersPanel } from '../../features/orders/OrdersPanel';

export function OrdersPage() {
  return (
    <>
      <Typography.Title level={3}>Đơn hàng</Typography.Title>
      <OrdersPanel />
    </>
  );
}
