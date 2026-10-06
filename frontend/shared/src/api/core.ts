import type { Paged } from '../types/common';
import type {
  CreatePurchaseReceiptRequest,
  LedgerFilter,
  LedgerLine,
  Order,
  PosCheckoutRequest,
  PosSku,
  PurchaseReceipt,
  PurchaseReceiptFilter,
  SetThresholdRequest,
  StockFilter,
  StockRow,
  Supplier,
  SupplierRequest,
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
