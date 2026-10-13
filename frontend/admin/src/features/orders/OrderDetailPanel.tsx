import {
  ApiError,
  errorMessage,
  formatDateTime,
  formatMoney,
  getOrder,
  type OrderItem,
  type ReservationStatus,
  transitionOrder,
  useAuth,
} from '@oism/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Descriptions, Flex, message, Popconfirm, Spin, Table, type TableColumnsType, Tag, Typography } from 'antd';
import { Navigate } from 'react-router-dom';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { DeliverySlip } from './DeliverySlip';
import { CHANNEL_LABELS, OrderStatusTag } from './labels';

type Action = 'confirm' | 'complete' | 'cancel';

const DONE: Record<Action, string> = { confirm: 'Đã duyệt đơn', complete: 'Đã hoàn tất đơn', cancel: 'Đã hủy đơn' };

const HOLD_LABELS: Record<ReservationStatus, string> = { Active: 'Đang giữ', Consumed: 'Đã xuất kho', Released: 'Đã trả về tồn' };

const CANCEL_REASONS = { Manual: 'Nhân viên hủy', Expired: 'Hết hạn giữ hàng' } as const;

const METHOD_LABELS = { Cash: 'Tiền mặt', QR: 'Chuyển khoản QR' } as const;

// UC-ORD-03, 04, 06: chi tiết một đơn với các bước duyệt, hoàn tất, hủy và in phiếu giao. Nút chỉ hiện theo trạng thái
// cho gọn; bước nào hợp lệ là do backend quyết định, và đơn có thể vừa đổi trạng thái ở nơi khác (job hết hạn).
export function OrderDetailPanel({ orderId }: { orderId: string }) {
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

  const holds = new Map((order.reservations ?? []).map((hold) => [hold.orderItemId, hold.status]));
  const busy = (action: Action) => act.isPending && act.variables === action;
  const columns: TableColumnsType<OrderItem> = [
    { title: 'Mã SKU', dataIndex: 'skuCode' },
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
    {
      title: 'Giữ hàng',
      render: (_, item) => {
        const status = holds.get(item.id);
        return status ? <Tag>{HOLD_LABELS[status]}</Tag> : '';
      },
    },
  ];

  return (
    <>
      <div className="no-print">
        <Flex justify="space-between" align="center" gap={16} wrap style={{ marginBlock: 16 }}>
          <Typography.Title level={3} style={{ margin: 0 }}>
            Đơn {order.orderNumber} <OrderStatusTag status={order.status} />
          </Typography.Title>
          <Flex gap={8} wrap>
            {order.status === 'Reserved' && (
              <>
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
              </>
            )}
            {order.status === 'Confirmed' && (
              <Button type="primary" loading={busy('complete')} onClick={() => act.mutate('complete')}>
                Hoàn tất
              </Button>
            )}
            {(order.status === 'Confirmed' || order.status === 'Completed') && <Button onClick={() => window.print()}>In phiếu giao</Button>}
          </Flex>
        </Flex>
        <Descriptions
          bordered
          size="small"
          column={{ xs: 1, md: 2 }}
          style={{ marginBottom: 16 }}
          items={[
            { key: 'channel', label: 'Kênh', children: CHANNEL_LABELS[order.channel] },
            { key: 'external', label: 'Mã đơn của sàn', children: order.externalOrderId ?? '' },
            { key: 'branch', label: 'Chi nhánh', children: branches.name(order.branchId) },
            { key: 'total', label: 'Tổng tiền', children: <strong>{formatMoney(order.totalAmount)}</strong> },
            { key: 'created', label: 'Tạo lúc', children: formatDateTime(order.createdAt) },
            ...(order.status === 'Reserved' && order.reservedUntil
              ? [{ key: 'until', label: 'Giữ hàng tới', children: formatDateTime(order.reservedUntil) }]
              : []),
            ...(order.confirmedAt ? [{ key: 'confirmed', label: 'Duyệt lúc', children: formatDateTime(order.confirmedAt) }] : []),
            ...(order.completedAt ? [{ key: 'completed', label: 'Hoàn tất lúc', children: formatDateTime(order.completedAt) }] : []),
            ...(order.cancelledAt
              ? [
                  {
                    key: 'cancelled',
                    label: 'Hủy lúc',
                    children: `${formatDateTime(order.cancelledAt)}${order.cancelReason ? ` · ${CANCEL_REASONS[order.cancelReason]}` : ''}`,
                  },
                ]
              : []),
            ...(order.payment
              ? [{ key: 'payment', label: 'Thanh toán', children: `${METHOD_LABELS[order.payment.method]} · ${formatMoney(order.payment.amount)}` }]
              : []),
            ...(order.note ? [{ key: 'note', label: 'Ghi chú', children: order.note }] : []),
          ]}
        />
        <Table<OrderItem> scroll={{ x: 'max-content' }} rowKey="id" pagination={false} dataSource={order.items} columns={columns} />
      </div>
      <DeliverySlip order={order} branchName={branches.name(order.branchId)} />
    </>
  );
}
