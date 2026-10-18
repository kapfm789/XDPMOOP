import {
  ApiError,
  errorMessage,
  formatDateTime,
  formatMoney,
  getOrder,
  type Order,
  type OrderItem,
  type Reservation,
  type ReservationStatus,
  transitionOrder,
  useAuth,
} from '@oism/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Flex,
  message,
  Popconfirm,
  Row,
  Spin,
  Steps,
  type StepsProps,
  Table,
  type TableColumnsType,
  Typography,
} from 'antd';
import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { StatusTag, type Tone } from '../common/StatusTag';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { DeliverySlip } from './DeliverySlip';
import { ChannelTag, OrderStatusTag } from './labels';

type Action = 'confirm' | 'complete' | 'cancel';

// Đầu trang do màn hình dựng (breadcrumb, tiêu đề); panel đưa tiêu đề, tag và các nút hành động của đơn.
export type OrderHeaderProps = { title: string; tags: ReactNode; extra: ReactNode };

const DONE: Record<Action, string> = { confirm: 'Đã duyệt đơn', complete: 'Đã hoàn tất đơn', cancel: 'Đã hủy đơn' };

const HOLDS: Record<ReservationStatus, { label: string; tone: Tone }> = {
  Active: { label: 'Đang giữ', tone: 'pending' },
  Consumed: { label: 'Đã xuất kho', tone: 'success' },
  Released: { label: 'Đã trả về tồn', tone: 'neutral' },
};

const CANCEL_REASONS = { Manual: 'Nhân viên hủy', Expired: 'Hết hạn giữ hàng' } as const;

const METHOD_LABELS = { Cash: 'Tiền mặt', QR: 'Chuyển khoản QR' } as const;

// Thanh bước Tạo, Giữ hàng, Đã duyệt, Hoàn tất (docs/design/ui/admin.md); đơn hủy dừng ở bước hủy.
function steps(order: Order): StepsProps['items'] {
  const at = (iso?: string | null) => (iso ? formatDateTime(iso) : undefined);
  const created = { title: 'Tạo', content: at(order.createdAt) };
  const reserved = {
    title: 'Giữ hàng',
    content: order.status === 'Reserved' && order.reservedUntil ? `Giữ tới ${formatDateTime(order.reservedUntil)}` : undefined,
  };
  if (order.status === 'Cancelled') {
    const reason = order.cancelReason ? CANCEL_REASONS[order.cancelReason] : undefined;
    return [
      { ...created, status: 'finish' },
      { ...reserved, status: 'finish' },
      { title: 'Đã hủy', status: 'error', content: [at(order.cancelledAt), reason].filter(Boolean).join(' · ') },
    ];
  }
  const reached = { Draft: 0, Reserved: 1, Confirmed: 2, Completed: 3 }[order.status];
  const status = (index: number) => (index < reached || order.status === 'Completed' ? 'finish' : index === reached ? 'process' : 'wait');
  return [
    { ...created, status: status(0) },
    { ...reserved, status: status(1) },
    { title: 'Đã duyệt', content: at(order.confirmedAt), status: status(2) },
    { title: 'Hoàn tất', content: at(order.completedAt), status: status(3) },
  ];
}

// UC-ORD-03, 04, 06: chi tiết một đơn với các bước duyệt, hoàn tất, hủy và in phiếu giao. Nút chỉ hiện theo trạng thái
// cho gọn; bước nào hợp lệ là do backend quyết định, và đơn có thể vừa đổi trạng thái ở nơi khác (job hết hạn).
export function OrderDetailPanel({ orderId, header }: { orderId: string; header: (props: OrderHeaderProps) => ReactNode }) {
  const queryClient = useQueryClient();
  const isOwner = useAuth().user?.role === 'Owner';
  const branches = useBranchOptions();
  const { data: order, error } = useQuery({ queryKey: ['order', orderId], queryFn: () => getOrder(orderId), retry: false });

  const act = useMutation({
    mutationFn: (action: Action) => transitionOrder(orderId, action),
    onSuccess: async (_, action) => {
      void message.success(DONE[action]);
      // Duyệt làm đổi tồn và thêm dòng sổ; hủy trả hàng đã giữ về tồn khả dụng.
      await Promise.all(['order', 'orders', 'stock', 'ledger'].map((key) => queryClient.invalidateQueries({ queryKey: [key] })));
    },
    onError: (failure) => {
      void message.error(errorMessage(failure));
      // Trạng thái đã đổi ở nơi khác: tải lại đơn (docs/conventions/frontend.md).
      void queryClient.invalidateQueries({ queryKey: ['order', orderId] });
    },
  });

  // Đơn không có hoặc thuộc tenant khác: đưa về danh sách (docs/conventions/frontend.md).
  if (error instanceof ApiError && error.code === 'not_found') return <Navigate to="/orders" replace />;
  if (error) return <Alert type="error" showIcon title={errorMessage(error)} />;
  if (!order) return <Spin />;

  const items = new Map(order.items.map((item) => [item.id, item]));
  const busy = (action: Action) => act.isPending && act.variables === action;
  const columns: TableColumnsType<OrderItem> = [
    { title: 'Mã SKU', dataIndex: 'skuCode', render: (skuCode: string) => <Typography.Text strong>{skuCode}</Typography.Text> },
    { title: 'Tên', dataIndex: 'skuName' },
    { title: 'Số lượng', dataIndex: 'quantity', align: 'right' },
    { title: 'Đơn giá', dataIndex: 'unitPrice', align: 'right', render: (unitPrice: number) => formatMoney(unitPrice) },
    { title: 'Giảm giá', dataIndex: 'discount', align: 'right', render: (discount: number) => formatMoney(discount) },
    // Số chính thức là tổng tiền backend trả; cột này chỉ để đọc từng dòng.
    { title: 'Thành tiền', align: 'right', render: (_, item) => formatMoney(item.quantity * item.unitPrice - item.discount) },
    // Giá vốn chỉ có trong phản hồi cho Owner, sau khi đơn được duyệt.
    ...(isOwner
      ? ([
          {
            title: 'Giá vốn',
            dataIndex: 'costPrice',
            align: 'right',
            render: (costPrice?: number) => (costPrice === undefined ? '' : formatMoney(costPrice)),
          },
        ] satisfies TableColumnsType<OrderItem>)
      : []),
  ];

  const actions = (
    <>
      {order.status === 'Reserved' && (
        <>
          <Popconfirm
            title="Hủy đơn?"
            description="Hàng đang giữ cho đơn sẽ quay lại bán được."
            okText="Hủy đơn"
            cancelText="Không"
            onConfirm={() => act.mutate('cancel')}
          >
            <Button danger loading={busy('cancel')}>
              Hủy đơn
            </Button>
          </Popconfirm>
          <Popconfirm
            title="Duyệt đơn?"
            description="Hàng sẽ được xuất kho và giá vốn được chốt. Đơn đã duyệt không hủy được."
            okText="Duyệt"
            cancelText="Không"
            onConfirm={() => act.mutate('confirm')}
          >
            <Button type="primary" loading={busy('confirm')}>
              Duyệt đơn
            </Button>
          </Popconfirm>
        </>
      )}
      {(order.status === 'Confirmed' || order.status === 'Completed') && <Button onClick={() => window.print()}>In phiếu giao</Button>}
      {order.status === 'Confirmed' && (
        <Button type="primary" loading={busy('complete')} onClick={() => act.mutate('complete')}>
          Hoàn tất
        </Button>
      )}
    </>
  );

  return (
    <>
      <div className="no-print">
        {header({
          title: `Đơn ${order.orderNumber}`,
          tags: (
            <Flex gap={4}>
              <OrderStatusTag status={order.status} />
              <ChannelTag channel={order.channel} />
            </Flex>
          ),
          extra: actions,
        })}
        <Card style={{ marginBottom: 16 }}>
          <Steps items={steps(order)} />
        </Card>
        <Row gutter={[16, 16]}>
          <Col xs={24} xl={16}>
            <Card title="Hàng trong đơn">
              <Table<OrderItem>
                scroll={{ x: 'max-content' }}
                rowKey="id"
                pagination={false}
                dataSource={order.items}
                columns={columns}
                summary={() => (
                  <Table.Summary.Row>
                    <Table.Summary.Cell index={0} colSpan={5} align="right">
                      <Typography.Text strong>Tổng tiền</Typography.Text>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={5} align="right">
                      <Typography.Text strong style={{ fontSize: 16 }}>
                        {formatMoney(order.totalAmount)}
                      </Typography.Text>
                    </Table.Summary.Cell>
                    {isOwner && <Table.Summary.Cell index={6} />}
                  </Table.Summary.Row>
                )}
              />
            </Card>
          </Col>
          <Col xs={24} xl={8}>
            <Flex vertical gap={16}>
              <Card title="Thông tin đơn">
                <Descriptions
                  size="small"
                  column={1}
                  items={[
                    { key: 'channel', label: 'Kênh', children: <ChannelTag channel={order.channel} /> },
                    ...(order.externalOrderId ? [{ key: 'external', label: 'Mã đơn của sàn', children: order.externalOrderId }] : []),
                    { key: 'branch', label: 'Chi nhánh', children: branches.name(order.branchId) },
                    { key: 'created', label: 'Tạo lúc', children: formatDateTime(order.createdAt) },
                    ...(order.note ? [{ key: 'note', label: 'Ghi chú', children: order.note }] : []),
                  ]}
                />
              </Card>
              <Card title="Giữ hàng">
                <Table<Reservation>
                  size="small"
                  rowKey="id"
                  pagination={false}
                  dataSource={order.reservations}
                  locale={{ emptyText: 'Đơn không có phần giữ hàng' }}
                  columns={[
                    { title: 'SKU', dataIndex: 'orderItemId', render: (id: string) => items.get(id)?.skuCode ?? '' },
                    { title: 'Số lượng', dataIndex: 'quantity', align: 'right' },
                    {
                      title: 'Trạng thái',
                      dataIndex: 'status',
                      render: (status: ReservationStatus, hold) => (
                        <Flex vertical gap={2}>
                          <span>
                            <StatusTag tone={HOLDS[status].tone}>{HOLDS[status].label}</StatusTag>
                          </span>
                          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {status === 'Active' ? `Hết hạn ${formatDateTime(hold.expiresAt)}` : hold.closedAt ? formatDateTime(hold.closedAt) : ''}
                          </Typography.Text>
                        </Flex>
                      ),
                    },
                  ]}
                />
              </Card>
              {/* Chỉ đơn POS có thanh toán (ADR-0007). */}
              {order.payment && (
                <Card title="Thanh toán">
                  <Descriptions
                    size="small"
                    column={1}
                    items={[
                      { key: 'method', label: 'Hình thức', children: METHOD_LABELS[order.payment.method] },
                      { key: 'total', label: 'Tổng đơn', children: formatMoney(order.totalAmount) },
                      { key: 'amount', label: 'Khách đưa', children: <strong>{formatMoney(order.payment.amount)}</strong> },
                      { key: 'at', label: 'Thu lúc', children: formatDateTime(order.payment.confirmedAt) },
                    ]}
                  />
                </Card>
              )}
            </Flex>
          </Col>
        </Row>
      </div>
      <DeliverySlip order={order} branchName={branches.name(order.branchId)} />
    </>
  );
}
