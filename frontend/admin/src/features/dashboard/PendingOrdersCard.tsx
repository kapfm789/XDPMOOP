import { errorMessage, formatDateTime, formatMoney, type Order } from '@oism/shared';
import { Alert, Card, Table } from 'antd';
import { Link } from 'react-router-dom';
import { DocumentCode } from '../common/StatusTag';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { ChannelTag } from '../orders/labels';
import { LIST_CARD_BODY_STYLE, LIST_CARD_STYLE, LIST_SCROLL_HEIGHT, PENDING_LIMIT, usePendingOrders } from './useDashboardData';

// Đơn đang giữ hàng chờ duyệt; thông báo OrderCreated làm danh sách tải lại.
export function PendingOrdersCard() {
  const branches = useBranchOptions();
  const orders = usePendingOrders();
  const error = orders.error ?? branches.error;
  const total = orders.data?.total ?? 0;

  return (
    <Card
      title={`Đơn chờ duyệt${orders.data ? ` (${total})` : ''}`}
      extra={<Link to="/orders">{total > PENDING_LIMIT ? 'Xem tất cả' : 'Đơn hàng'}</Link>}
      style={LIST_CARD_STYLE}
      styles={{ body: LIST_CARD_BODY_STYLE }}
    >
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<Order>
        size="small"
        scroll={{ x: 'max-content', y: LIST_SCROLL_HEIGHT }}
        rowKey="id"
        loading={orders.isPending}
        dataSource={orders.data?.items}
        pagination={false}
        locale={{ emptyText: 'Không có đơn nào chờ duyệt' }}
        columns={[
          { title: 'Mã đơn', dataIndex: 'orderNumber', render: (number: string, order) => <DocumentCode code={number} to={`/orders/${order.id}`} /> },
          { title: 'Kênh', dataIndex: 'channel', render: (channel: Order['channel']) => <ChannelTag channel={channel} /> },
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          { title: 'Tổng tiền', dataIndex: 'totalAmount', align: 'right', render: (amount: number) => formatMoney(amount) },
          { title: 'Giữ tới', dataIndex: 'reservedUntil', render: (at?: string | null) => (at ? formatDateTime(at) : '') },
        ]}
      />
    </Card>
  );
}
