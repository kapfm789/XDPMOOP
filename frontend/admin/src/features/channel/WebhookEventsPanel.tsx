import { errorMessage, formatDateTime, listWebhookEvents, type SalesChannel, type WebhookEvent, type WebhookStatus } from '@oism/shared';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Select, Table, Typography } from 'antd';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { FilterBar } from '../common/FilterBar';
import { StatusTag, type Tone } from '../common/StatusTag';
import { ChannelTag } from '../orders/labels';
import { SALES_CHANNEL_OPTIONS } from './ChannelShopsPanel';

type Filter = { channel?: SalesChannel; status?: WebhookStatus; page: number };

const PAGE_SIZE = 20;

const STATUS_LABELS: Record<WebhookStatus, string> = {
  Received: 'Đã nhận',
  Submitted: 'Chờ kho xử lý',
  Reserved: 'Đã giữ hàng',
  Rejected: 'Bị từ chối',
  Failed: 'Payload lỗi',
};

const STATUS_TONES: Record<WebhookStatus, Tone> = {
  Received: 'neutral',
  Submitted: 'pending',
  Reserved: 'success',
  Rejected: 'error',
  Failed: 'error',
};

// Mã lý do của OrderRejected (docs/design/events.md). Lý do của bản ghi Failed là câu mô tả sẵn từ backend.
const REJECT_REASONS: Record<string, string> = {
  InsufficientStock: 'Không đủ tồn khả dụng',
  UnknownSku: 'Mã SKU không có ở kho',
  InactiveSku: 'SKU đã ngừng bán',
  InactiveBranch: 'Chi nhánh của shop chưa có ở kho hoặc đã tắt',
};

const STATUS_OPTIONS = (Object.keys(STATUS_LABELS) as WebhookStatus[])
  // Received chỉ tồn tại bên trong transaction nhận webhook.
  .filter((value) => value !== 'Received')
  .map((value) => ({ value, label: STATUS_LABELS[value] }));

// UC-SIM-01 AC-6: nhật ký webhook đã nhận, kèm trạng thái và lý do khi bị từ chối.
export function WebhookEventsPanel() {
  const [filter, setFilter] = useState<Filter>({ page: 1 });
  const events = useQuery({
    queryKey: ['webhook-events', filter],
    queryFn: () => listWebhookEvents({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const narrow = (change: Partial<Filter>) => setFilter((current) => ({ ...current, ...change, page: 1 }));

  return (
    <>
      <FilterBar>
        <Select
          allowClear
          placeholder="Mọi kênh"
          aria-label="Lọc theo kênh"
          options={SALES_CHANNEL_OPTIONS}
          value={filter.channel}
          onChange={(channel?: SalesChannel) => narrow({ channel })}
          style={{ width: 140 }}
        />
        <Select
          allowClear
          placeholder="Mọi trạng thái"
          aria-label="Lọc theo trạng thái"
          options={STATUS_OPTIONS}
          value={filter.status}
          onChange={(status?: WebhookStatus) => narrow({ status })}
          style={{ width: 180 }}
        />
      </FilterBar>
      {/* Kết quả giữ hàng về sau bằng event, nên bản ghi vừa gửi còn ở trạng thái chờ trong vài giây. */}
      <Card
        title="Webhook đã nhận"
        extra={
          <Button loading={events.isFetching} onClick={() => void events.refetch()}>
            Tải lại
          </Button>
        }
      >
        {events.error && <Alert type="error" showIcon title={errorMessage(events.error)} style={{ marginBottom: 16 }} />}
        <Table<WebhookEvent>
          scroll={{ x: 'max-content' }}
          rowKey="id"
          loading={events.isPending}
          dataSource={events.data?.items}
          pagination={{
            current: filter.page,
            pageSize: PAGE_SIZE,
            total: events.data?.total,
            showSizeChanger: false,
            onChange: (page) => setFilter((current) => ({ ...current, page })),
          }}
          columns={[
            { title: 'Nhận lúc', dataIndex: 'receivedAt', render: (receivedAt: string) => formatDateTime(receivedAt) },
            { title: 'Kênh', dataIndex: 'channel', render: (channel: SalesChannel) => <ChannelTag channel={channel} /> },
            { title: 'Mã sự kiện', dataIndex: 'eventId', render: (eventId: string) => <Typography.Text strong>{eventId}</Typography.Text> },
            { title: 'Mã đơn của sàn', dataIndex: 'externalOrderId' },
            {
              title: 'Trạng thái',
              dataIndex: 'status',
              render: (status: WebhookStatus) => <StatusTag tone={STATUS_TONES[status]}>{STATUS_LABELS[status]}</StatusTag>,
            },
            { title: 'Lý do', dataIndex: 'reason', render: (reason?: string | null) => (reason ? (REJECT_REASONS[reason] ?? reason) : '') },
            { title: 'Đơn hàng', dataIndex: 'orderId', render: (orderId?: string | null) => orderId && <Link to={`/orders/${orderId}`}>Xem đơn</Link> },
          ]}
        />
      </Card>
    </>
  );
}
