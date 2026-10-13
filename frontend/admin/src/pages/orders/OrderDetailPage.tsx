import { Link, useParams } from 'react-router-dom';
import { OrderDetailPanel } from '../../features/orders/OrderDetailPanel';

export function OrderDetailPage() {
  const { id } = useParams();

  return (
    <>
      <Link to="/orders" className="no-print">
        ← Danh sách đơn hàng
      </Link>
      {id && <OrderDetailPanel key={id} orderId={id} />}
    </>
  );
}
