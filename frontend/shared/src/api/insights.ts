import type {
  GrossProfitFilter,
  GrossProfitReport,
  InventoryValueFilter,
  InventoryValueReport,
  LowStockAlert,
  ReorderSuggestionFilter,
  ReorderSuggestions,
  SalesWindowFilter,
  SlowMoverRow,
  TopSellerRow,
} from '../types/insights';
import { api } from './client';

// Chuỗi truy vấn chỉ gồm các tham số có giá trị.
function toQuery(filter: Record<string, string | number | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filter)) {
    if (value !== undefined && value !== '') params.set(key, String(value));
  }
  return params.toString();
}

// Chỉ Owner. Bỏ trống `groupBy` thì chỉ có `totals`.
export const getGrossProfit = (filter: GrossProfitFilter) =>
  api<GrossProfitReport>(`/api/insights/reports/gross-profit?${toQuery(filter)}`);

export const getInventoryValue = (filter: InventoryValueFilter) =>
  api<InventoryValueReport>(`/api/insights/reports/inventory-value?${toQuery(filter)}`);

export const listTopSellers = (filter: SalesWindowFilter) =>
  api<TopSellerRow[]>(`/api/insights/reports/top-sellers?${toQuery(filter)}`);

export const listSlowMovers = (filter: SalesWindowFilter) =>
  api<SlowMoverRow[]>(`/api/insights/reports/slow-movers?${toQuery(filter)}`);

export const listLowStockAlerts = (branchId?: string) =>
  api<LowStockAlert[]>(`/api/insights/alerts/low-stock?${toQuery({ branchId })}`);

// Chỉ Owner. Đề xuất chỉ là khuyến nghị: không có lời gọi nào biến nó thành phiếu nhập.
export const getReorderSuggestions = (filter: ReorderSuggestionFilter) =>
  api<ReorderSuggestions>(`/api/insights/forecast/reorder-suggestions?${toQuery(filter)}`);

// Trả về khi lần chạy đã được ghi, kể cả lần chạy lỗi; trạng thái xem ở đề xuất nhập.
export const runForecast = () => api<{ runId: string }>('/api/insights/forecast/run', { method: 'POST' });
