// Kiểu dữ liệu của insights: docs/design/api/insights.md. Thông báo SignalR: docs/design/events.md mục "Thông báo SignalR".

import type { OrderChannel } from './core';

export type OrderCreatedNotification = {
  orderId: string;
  orderNumber: string;
  channel: OrderChannel;
  branchId: string;
  totalAmount: number;
};

export type LowStockNotification = {
  branchId: string;
  skuId: string;
  // Để trống khi insights chưa nhận SkuUpserted của SKU đó.
  skuCode: string | null;
  name: string | null;
  available: number;
  threshold: number;
};
