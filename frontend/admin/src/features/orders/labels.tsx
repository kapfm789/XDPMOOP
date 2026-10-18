import type { OrderChannel, OrderStatus } from '@oism/shared';
import { Tag } from 'antd';
import { StatusTag, type Tone } from '../common/StatusTag';

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

const STATUS_TONES: Record<OrderStatus, Tone> = {
  Draft: 'neutral',
  Reserved: 'pending',
  Confirmed: 'info',
  Completed: 'success',
  Cancelled: 'error',
};

export function OrderStatusTag({ status }: { status: OrderStatus }) {
  return <StatusTag tone={STATUS_TONES[status]}>{STATUS_LABELS[status]}</StatusTag>;
}

// Shopee cam, TikTok đen, Lazada xanh đậm, POS và Admin xanh (docs/design/ui/admin.md mục "Bố cục và phong cách").
const CHANNEL_STYLES: Record<OrderChannel, { color: string; background: string; borderColor: string }> = {
  Shopee: { color: '#d4380d', background: '#fff2e8', borderColor: '#ffbb96' },
  TikTok: { color: '#141414', background: '#f0f0f0', borderColor: '#bfbfbf' },
  Lazada: { color: '#10239e', background: '#f0f5ff', borderColor: '#adc6ff' },
  POS: { color: '#1677ff', background: '#e6f4ff', borderColor: '#91caff' },
  Admin: { color: '#1677ff', background: '#e6f4ff', borderColor: '#91caff' },
};

export function ChannelTag({ channel }: { channel: OrderChannel }) {
  return <Tag style={CHANNEL_STYLES[channel]}>{CHANNEL_LABELS[channel]}</Tag>;
}
