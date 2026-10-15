import { errorMessage, formatDateTime, formatMoney, listOrders, type Order } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Table } from 'antd';
import { Link } from 'react-router-dom';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { CHANNEL_LABELS } from '../orders/labels';

const LIMIT = 10;

// Đơn đang giữ hàng chờ duyệt. Khóa truy vấn bắt đầu bằng 'orders' nên thông báo OrderCreated làm nó tải lại.
export function PendingOrdersCard() {
  const branches = useBranchOptions();
  const orders = useQuery({
    queryKey: ['orders', 'pending'],
    queryFn: () => listOrders({ status: 'Reserved', page: 1, pageSize: LIMIT }),
  });
  const error = orders.error ?? branches.error;
  const total = orders.data?.total ?? 0;

  return (
    <Card
      title={`Đơn chờ duyệt${orders.data ? ` (${total})` : ''}`}
      extra={total > LIMIT ? <Link to="/orders">Xem tất cả</Link> : <Link to="/orders">Đơn hàng</Link>}
    >
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<Order>
        size="small"
        scroll={{ x: 'max-content' }}
        rowKey="id"
        loading={orders.isPending}
        dataSource={orders.data?.items}
        pagination={false}
        locale={{ emptyText: 'Không có đơn nào chờ duyệt' }}
        columns={[
          { title: 'Mã đơn', dataIndex: 'orderNumber', render: (number: string, order) => <Link to={`/orders/${order.id}`}>{number}</Link> },
          { title: 'Kênh', dataIndex: 'channel', render: (channel: Order['channel']) => CHANNEL_LABELS[channel] },
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          { title: 'Tổng tiền', dataIndex: 'totalAmount', align: 'right', render: (amount: number) => formatMoney(amount) },
          { title: 'Giữ tới', dataIndex: 'reservedUntil', render: (at?: string | null) => (at ? formatDateTime(at) : '') },
        ]}
      />
    </Card>
  );
}
