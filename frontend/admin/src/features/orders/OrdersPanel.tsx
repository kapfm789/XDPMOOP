import { errorMessage, formatDateTime, formatMoney, listOrders, type Order, type OrderChannel, type OrderStatus } from '@oism/shared';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Alert, Button, DatePicker, Flex, Select, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { CHANNEL_LABELS, OrderStatusTag, STATUS_LABELS } from './labels';

type Filter = { status?: OrderStatus; channel?: OrderChannel; branchId?: string; from?: string; to?: string; page: number };

const PAGE_SIZE = 20;

const STATUS_OPTIONS = (['Reserved', 'Confirmed', 'Completed', 'Cancelled'] as const).map((value) => ({ value, label: STATUS_LABELS[value] }));

const CHANNEL_OPTIONS = (Object.keys(CHANNEL_LABELS) as OrderChannel[]).map((value) => ({ value, label: CHANNEL_LABELS[value] }));

// UC-ORD-06 AC-4: đơn của mọi kênh ở chung một danh sách, lọc theo trạng thái, kênh, chi nhánh và khoảng thời gian.
export function OrdersPanel() {
  const navigate = useNavigate();
  const [filter, setFilter] = useState<Filter>({ page: 1 });
  const branches = useBranchOptions();
  const orders = useQuery({
    queryKey: ['orders', filter],
    queryFn: () => listOrders({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const error = orders.error ?? branches.error;
  const narrow = (change: Partial<Filter>) => setFilter((current) => ({ ...current, ...change, page: 1 }));

  return (
    <>
      <Flex justify="space-between" gap={16} wrap style={{ marginBottom: 16 }}>
        <Flex gap={8} wrap>
          <Select
            allowClear
            placeholder="Mọi trạng thái"
            aria-label="Lọc theo trạng thái"
            options={STATUS_OPTIONS}
            value={filter.status}
            onChange={(status?: OrderStatus) => narrow({ status })}
            style={{ width: 160 }}
          />
          <Select
            allowClear
            placeholder="Mọi kênh"
            aria-label="Lọc theo kênh"
            options={CHANNEL_OPTIONS}
            value={filter.channel}
            onChange={(channel?: OrderChannel) => narrow({ channel })}
            style={{ width: 140 }}
          />
          <Select
            allowClear
            placeholder="Mọi chi nhánh"
            aria-label="Lọc theo chi nhánh"
            options={branches.options}
            value={filter.branchId}
            onChange={(branchId?: string) => narrow({ branchId })}
            style={{ width: 240 }}
          />
          <DatePicker.RangePicker
            aria-label="Lọc theo ngày tạo đơn"
            format="DD/MM/YYYY"
            // Tính cả hai đầu: từ đầu ngày bắt đầu tới hết ngày kết thúc, theo giờ của máy đang dùng.
            onChange={(range) =>
              narrow({ from: range?.[0]?.startOf('day').toISOString(), to: range?.[1]?.endOf('day').toISOString() })
            }
          />
        </Flex>
        <Space>
          {/* Đơn từ sàn tới qua event; thông báo đẩy về trình duyệt thuộc W4-08, tới lúc đó thì tải lại bằng tay. */}
          <Button loading={orders.isFetching} onClick={() => void orders.refetch()}>
            Tải lại
          </Button>
          <Button type="primary" onClick={() => navigate('/orders/new')}>
            Tạo đơn
          </Button>
        </Space>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<Order>
        scroll={{ x: 'max-content' }}
        rowKey="id"
        loading={orders.isPending}
        dataSource={orders.data?.items}
        pagination={{
          current: filter.page,
          pageSize: PAGE_SIZE,
          total: orders.data?.total,
          showSizeChanger: false,
          onChange: (page) => setFilter((current) => ({ ...current, page })),
        }}
        columns={[
          { title: 'Mã đơn', dataIndex: 'orderNumber', render: (orderNumber: string, order) => <Link to={`/orders/${order.id}`}>{orderNumber}</Link> },
          { title: 'Kênh', dataIndex: 'channel', render: (channel: OrderChannel) => <Tag>{CHANNEL_LABELS[channel]}</Tag> },
          { title: 'Mã đơn của sàn', dataIndex: 'externalOrderId' },
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          { title: 'Số dòng', align: 'right', render: (_, order) => order.items.length },
          { title: 'Tổng tiền', dataIndex: 'totalAmount', align: 'right', render: (total: number) => formatMoney(total) },
          { title: 'Trạng thái', dataIndex: 'status', render: (status: OrderStatus) => <OrderStatusTag status={status} /> },
          {
            title: 'Giữ hàng tới',
            render: (_, order) => (order.status === 'Reserved' && order.reservedUntil ? formatDateTime(order.reservedUntil) : ''),
          },
          { title: 'Tạo lúc', dataIndex: 'createdAt', render: (createdAt: string) => formatDateTime(createdAt) },
        ]}
      />
    </>
  );
}
