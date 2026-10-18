import { PageHeader } from '../../app/PageHeader';
import { NewOrderForm } from '../../features/orders/NewOrderForm';

export function NewOrderPage() {
  return (
    <>
      <PageHeader title="Tạo đơn thủ công" />
      <NewOrderForm />
    </>
  );
}
