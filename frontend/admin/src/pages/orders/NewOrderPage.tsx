import { Typography } from 'antd';
import { Link } from 'react-router-dom';
import { NewOrderForm } from '../../features/orders/NewOrderForm';

export function NewOrderPage() {
  return (
    <>
      <Link to="/orders">← Danh sách đơn hàng</Link>
      <Typography.Title level={3}>Tạo đơn thủ công</Typography.Title>
      <NewOrderForm />
    </>
  );
}
