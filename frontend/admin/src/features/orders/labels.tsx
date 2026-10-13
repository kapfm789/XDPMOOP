import type { OrderChannel, OrderStatus } from '@oism/shared';
import { Tag } from 'antd';

export const CHANNEL_LABELS: Record<OrderChannel, string> = {
  POS: 'POS',
  Admin: 'Thủ công',
  Shopee: 'Shopee',
  TikTok: 'TikTok',
  Lazada: 'Lazada',
};

// Draft chỉ tồn tại bên trong transaction tạo đơn nên không có trong bộ lọc.
export const STATUS_LABELS: Record<OrderStatus, string> = {
  Draft: 'Nháp',
  Reserved: 'Chờ duyệt',
  Confirmed: 'Đã duyệt',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã hủy',
};

const STATUS_COLORS: Record<OrderStatus, string | undefined> = {
  Draft: undefined,
  Reserved: 'gold',
  Confirmed: 'blue',
  Completed: 'green',
  Cancelled: 'red',
};

export function OrderStatusTag({ status }: { status: OrderStatus }) {
  return <Tag color={STATUS_COLORS[status]}>{STATUS_LABELS[status]}</Tag>;
}
