// Kiểu dữ liệu của insights: docs/design/api/insights.md. Thông báo SignalR: docs/design/events.md mục "Thông báo SignalR".

import type { Paged } from './common';
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

export type GrossProfitGroupBy = 'day' | 'branch' | 'channel' | 'sku';

// `from` và `to` là ngày `yyyy-MM-dd` theo giờ Việt Nam, tính cả hai đầu.
export type GrossProfitFilter = {
  from: string;
  to: string;
  branchId?: string;
  channel?: OrderChannel;
  skuId?: string;
  groupBy?: GrossProfitGroupBy;
};

export type GrossProfitTotals = {
  quantity: number;
  revenue: number;
  cogs: number;
  grossProfit: number;
};

// `key` là ngày `yyyy-MM-dd`, `branchId`, tên kênh hoặc `skuId` tùy `groupBy`; `skuCode`, `name` chỉ có khi nhóm theo SKU.
export type GrossProfitRow = GrossProfitTotals & {
  key: string;
  skuCode: string | null;
  name: string | null;
};

export type GrossProfitReport = {
  totals: GrossProfitTotals;
  rows: GrossProfitRow[];
};

// `avgCost` và `value` là null với Staff.
export type InventoryValueRow = {
  branchId: string;
  skuId: string;
  skuCode: string | null;
  name: string | null;
  onHand: number;
  avgCost: number | null;
  value: number | null;
};

// `totalValue` là tổng của mọi dòng khớp bộ lọc; null với Staff.
export type InventoryValueReport = Paged<InventoryValueRow> & { totalValue: number | null };

export type InventoryValueFilter = {
  branchId?: string;
  page: number;
  pageSize: number;
};

// `days` là số ngày gần nhất tính cả hôm nay, mặc định 30; `limit` mặc định 20.
export type SalesWindowFilter = {
  days?: number;
  branchId?: string;
  limit?: number;
};

export type TopSellerRow = {
  skuId: string;
  skuCode: string | null;
  name: string | null;
  quantity: number;
};

export type SlowMoverRow = TopSellerRow & { onHand: number };

export type LowStockAlert = {
  branchId: string;
  skuId: string;
  skuCode: string | null;
  name: string | null;
  available: number;
  threshold: number;
};

export type ForecastRunStatus = 'Succeeded' | 'Failed';

// Hai WAPE là null khi lần chạy lỗi hoặc không có dữ liệu để đánh giá.
export type ForecastRun = {
  id: string;
  ranAt: string;
  method: string;
  status: ForecastRunStatus;
  error: string | null;
  forecastWape: number | null;
  baselineWape: number | null;
};

// Chưa đủ dữ liệu thì `forecastQty` và `suggestedReorderQty` là null.
export type ReorderSuggestion = {
  branchId: string;
  skuId: string;
  skuCode: string | null;
  name: string | null;
  hasEnoughData: boolean;
  forecastQty: number | null;
  available: number;
  suggestedReorderQty: number | null;
};

// `run` là lần chạy gần nhất, có thể lỗi; `items` lấy từ `lastSucceededRun`.
export type ReorderSuggestions = Paged<ReorderSuggestion> & {
  run: ForecastRun | null;
  lastSucceededRun: ForecastRun | null;
};

export type ReorderSuggestionFilter = {
  branchId?: string;
  page: number;
  pageSize: number;
};
