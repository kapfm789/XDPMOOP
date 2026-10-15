import type { Paged } from '../types/common';
import type {
  CreateOrderRequest,
  CreatePurchaseReceiptRequest,
  CreateTransferRequest,
  LedgerFilter,
  LedgerLine,
  Order,
  OrderFilter,
  PosCheckoutRequest,
  PosSku,
  PurchaseReceipt,
  PurchaseReceiptFilter,
  SetThresholdRequest,
  StockFilter,
  StockRow,
  Stocktake,
  StocktakeCount,
  StocktakeFilter,
  StockTransfer,
  Supplier,
  SupplierRequest,
  TransferFilter,
  UpdatePurchaseReceiptRequest,
} from '../types/core';
import { api } from './client';

// Chuỗi truy vấn chỉ gồm các tham số có giá trị.
function toQuery(filter: Record<string, string | number | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filter)) {
    if (value !== undefined && value !== '') params.set(key, String(value));
  }
  return params.toString();
}

export const listStock = ({ query, ...filter }: StockFilter) =>
  api<Paged<StockRow>>(`/api/core/stock?${toQuery({ ...filter, query: query?.trim() })}`);

export const setStockThreshold = (request: SetThresholdRequest) =>
  api<StockRow>('/api/core/stock/threshold', { method: 'PUT', body: request });

// Sổ chỉ có đường đọc: không có lời gọi nào sửa hay xóa dòng sổ.
export const listLedger = (filter: LedgerFilter) => api<Paged<LedgerLine>>(`/api/core/ledger?${toQuery(filter)}`);

export const listSuppliers = () => api<Supplier[]>('/api/core/suppliers');

export const createSupplier = (request: SupplierRequest) =>
  api<Supplier>('/api/core/suppliers', { method: 'POST', body: request });

export const listPurchaseReceipts = (filter: PurchaseReceiptFilter) =>
  api<Paged<PurchaseReceipt>>(`/api/core/purchase-receipts?${toQuery(filter)}`);

export const createPurchaseReceipt = (request: CreatePurchaseReceiptRequest) =>
  api<PurchaseReceipt>('/api/core/purchase-receipts', { method: 'POST', body: request });

export const updatePurchaseReceipt = (id: string, request: UpdatePurchaseReceiptRequest) =>
  api<PurchaseReceipt>(`/api/core/purchase-receipts/${id}`, { method: 'PUT', body: request });

// Xác nhận lại phiếu đã Confirmed trả lại cùng phiếu, không tác động lần hai.
export const confirmPurchaseReceipt = (id: string) =>
  api<PurchaseReceipt>(`/api/core/purchase-receipts/${id}/confirm`, { method: 'POST' });

// Tối đa 20 SKU đang bán kèm tồn khả dụng tại chi nhánh. Tồn ở đây chỉ để báo sớm; checkout mới là nơi quyết định.
export const searchPosSkus = (branchId: string, query: string) =>
  api<PosSku[]>(`/api/core/pos/skus?${toQuery({ branchId, query: query.trim() })}`);

// Mỗi lần bấm thanh toán mang một `Idempotency-Key`; gửi lại cùng khóa nhận lại đúng đơn đã tạo.
export const posCheckout = (request: PosCheckoutRequest, idempotencyKey: string) =>
  api<Order>('/api/core/pos/checkout', { method: 'POST', body: request, headers: { 'Idempotency-Key': idempotencyKey } });

export const listOrders = (filter: OrderFilter) => api<Paged<Order>>(`/api/core/orders?${toQuery(filter)}`);

// Đơn kèm dòng đơn, phần giữ hàng và thanh toán.
export const getOrder = (id: string) => api<Order>(`/api/core/orders/${id}`);

export const createOrder = (request: CreateOrderRequest) => api<Order>('/api/core/orders', { method: 'POST', body: request });

// Duyệt, hoàn tất, hủy. Bước chuyển sai trạng thái trả 409 `invalid_state_transition`; hủy lại đơn đã hủy trả lại cùng đơn.
export const transitionOrder = (id: string, action: 'confirm' | 'complete' | 'cancel') =>
  api<Order>(`/api/core/orders/${id}/${action}`, { method: 'POST' });

export const listTransfers = (filter: TransferFilter) => api<Paged<StockTransfer>>(`/api/core/transfers?${toQuery(filter)}`);

export const createTransfer = (request: CreateTransferRequest) =>
  api<StockTransfer>('/api/core/transfers', { method: 'POST', body: request });

// Xuất trừ tồn khả dụng nơi gửi; thiếu thì 409 `insufficient_stock` và không dòng nào được xuất.
// Nhận cộng tồn nơi nhận; nhận lại phiếu đã Received trả lại cùng phiếu.
export const transitionTransfer = (id: string, action: 'ship' | 'receive') =>
  api<StockTransfer>(`/api/core/transfers/${id}/${action}`, { method: 'POST' });

// Chỉ phiếu còn Draft xóa được.
export const deleteTransfer = (id: string) => api<void>(`/api/core/transfers/${id}`, { method: 'DELETE' });

export const listStocktakes = (filter: StocktakeFilter) => api<Paged<Stocktake>>(`/api/core/stocktakes?${toQuery(filter)}`);

export const openStocktake = (branchId: string) => api<Stocktake>('/api/core/stocktakes', { method: 'POST', body: { branchId } });

export const setStocktakeCounts = (id: string, items: StocktakeCount[]) =>
  api<Stocktake>(`/api/core/stocktakes/${id}/counts`, { method: 'PUT', body: { items } });

// Chốt điều chỉnh tồn theo số đếm; số đếm dưới lượng đang giữ thì 409 `stocktake_below_reserved`.
export const postStocktake = (id: string) => api<Stocktake>(`/api/core/stocktakes/${id}/post`, { method: 'POST' });
