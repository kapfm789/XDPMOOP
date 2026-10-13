import type {
  BurstResult,
  ChannelShop,
  CreateChannelShopRequest,
  SimulateBurstRequest,
  WebhookEvent,
  WebhookEventFilter,
} from '../types/channel';
import type { Paged } from '../types/common';
import { api } from './client';

export const listChannelShops = () => api<ChannelShop[]>('/api/channel/channel-shops');

// Phản hồi mang `secret`; các lần đọc sau không trả lại nó.
export const createChannelShop = (request: CreateChannelShopRequest) =>
  api<ChannelShop>('/api/channel/channel-shops', { method: 'POST', body: request });

export const setChannelShopActive = (id: string, isActive: boolean) =>
  api<ChannelShop>(`/api/channel/channel-shops/${id}/active`, { method: 'PATCH', body: { isActive } });

export function listWebhookEvents({ channel, status, page, pageSize }: WebhookEventFilter) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (channel) params.set('channel', channel);
  if (status) params.set('status', status);
  return api<Paged<WebhookEvent>>(`/api/channel/webhook-events?${params}`);
}

// Trả về sau khi mọi webhook đã được gửi; kết quả giữ hàng của từng đơn xem ở danh sách webhook.
export const simulateBurst = (request: SimulateBurstRequest) =>
  api<BurstResult>('/api/channel/simulator/burst', { method: 'POST', body: request });
