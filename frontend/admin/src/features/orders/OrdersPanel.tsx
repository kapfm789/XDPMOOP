import { errorMessage, formatDateTime, formatMoney, formatNumber, listOrders, type Order, type OrderChannel, type OrderStatus } from '@oism/shared';
import { keepPreviousData, useQueries, useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, DatePicker, Flex, Select, Table, Typography } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { FilterBar } from '../common/FilterBar';
import { DocumentCode } from '../common/StatusTag';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { CHANNEL_LABELS, ChannelTag, OrderStatusTag, STATUS_LABELS } from './labels';

type Filter = { status?: OrderStatus; channel?: OrderChannel; branchId?: string; from?: string; to?: string; page: number };

const PAGE_SIZE = 20;

// Draft chỉ tồn tại bên trong transaction tạo đơn nên không có tab.
const STATUS_TABS: readonly (OrderStatus | 'all')[] = ['all', 'Reserved', 'Confirmed', 'Completed', 'Cancelled'];

const CHANNEL_OPTIONS = (Object.keys(CHANNEL_LABELS) as OrderChannel[]).map((value) => ({ value, label: CHANNEL_LABELS[value] }));

// UC-ORD-06 AC-4: đơn của mọi kênh ở chung một danh sách, lọc theo trạng thái, kênh, chi nhánh và khoảng thời gian.
// Trạng thái là tab kèm số đơn; số trên tab là `total` của lời gọi lọc theo trạng thái đó (docs/design/ui/admin.md).
export function OrdersPanel() {
  const navigate = useNavigate();
  const [filter, setFilter] = useState<Filter>({ page: 1 });
  const branches = useBranchOptions();
  const orders = useQuery({
    queryKey: ['orders', filter],
    queryFn: () => listOrders({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  // Khóa bắt đầu bằng 'orders' nên thông báo OrderCreated làm cả số trên tab tải lại.
  const { status: _status, page: _page, ...scope } = filter;
  const counts = useQueries({
    queries: STATUS_TABS.map((tab) => ({
      queryKey: ['orders', 'count', tab, scope],
      queryFn: () => listOrders({ ...scope, status: tab === 'all' ? undefined : tab, page: 1, pageSize: 1 }),
      placeholderData: keepPreviousData,
    })),
  });
  const error = orders.error ?? branches.error;
  const narrow = (change: Partial<Filter>) => setFilter((current) => ({ ...current, ...change, page: 1 }));

  return (
    <>
      <FilterBar
        extra={
          <Button loading={orders.isFetching} onClick={() => void orders.refetch()}>
            Tải lại
          </Button>
        }
      >
        <Select
          allowClear
          placeholder="Mọi kênh"
          aria-label="Lọc theo kênh"
          options={CHANNEL_OPTIONS}
          value={filter.channel}
          onChange={(channel?: OrderChannel) => narrow({ channel })}
          style={{ width: 160 }}
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
      </FilterBar>
      <Card
        title="Danh sách đơn"
        extra={
          <Button type="primary" onClick={() => navigate('/orders/new')}>
            Tạo đơn
          </Button>
        }
        activeTabKey={filter.status ?? 'all'}
        onTabChange={(key) => narrow({ status: key === 'all' ? undefined : (key as OrderStatus) })}
        tabList={STATUS_TABS.map((tab, index) => ({
          key: tab,
          label: (
            <Flex align="center" gap={6}>
              {tab === 'all' ? 'Tất cả' : STATUS_LABELS[tab]}
              {counts[index].data && (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {formatNumber(counts[index].data.total)}
                </Typography.Text>
              )}
            </Flex>
          ),
        }))}
      >
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
            { title: 'Mã đơn', dataIndex: 'orderNumber', render: (orderNumber: string, order) => <DocumentCode code={orderNumber} to={`/orders/${order.id}`} /> },
            { title: 'Kênh', dataIndex: 'channel', render: (channel: OrderChannel) => <ChannelTag channel={channel} /> },
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
      </Card>
    </>
  );
}
