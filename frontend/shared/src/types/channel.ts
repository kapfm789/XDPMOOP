// Kiểu dữ liệu của API channel: docs/design/api/channel.md.

export type SalesChannel = 'Shopee' | 'TikTok' | 'Lazada';

// `secret` chỉ có trong phản hồi lúc tạo shop.
export type ChannelShop = {
  id: string;
  channel: SalesChannel;
  shopId: string;
  branchId: string;
  isActive: boolean;
  secret?: string;
};

export type CreateChannelShopRequest = {
  channel: SalesChannel;
  shopId: string;
  branchId: string;
};

export type WebhookStatus = 'Received' | 'Submitted' | 'Reserved' | 'Rejected' | 'Failed';

// `reason` là mã lý do của `core` khi Rejected, và là câu mô tả lỗi của payload khi Failed.
export type WebhookEvent = {
  id: string;
  channel: SalesChannel;
  eventId: string;
  externalOrderId?: string | null;
  status: WebhookStatus;
  reason?: string | null;
  orderId?: string | null;
  receivedAt: string;
  updatedAt: string;
};

export type WebhookEventFilter = {
  channel?: SalesChannel;
  status?: WebhookStatus;
  page: number;
  pageSize: number;
};

// `unitPrice` bỏ trống là 0: channel không biết giá của SKU.
export type SimulateBurstRequest = {
  channel: SalesChannel;
  shopId: string;
  skuCode: string;
  quantityPerOrder: number;
  orders: number;
  concurrency: number;
  unitPrice?: number;
};

export type BurstResult = {
  burstId: string;
  sent: number;
};
