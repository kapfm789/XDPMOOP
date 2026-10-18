import { useParams } from 'react-router-dom';
import { PageHeader } from '../../app/PageHeader';
import { OrderDetailPanel } from '../../features/orders/OrderDetailPanel';

export function OrderDetailPage() {
  const { id } = useParams();

  return id && <OrderDetailPanel key={id} orderId={id} header={(props) => <PageHeader crumb={props.title} {...props} />} />;
}
