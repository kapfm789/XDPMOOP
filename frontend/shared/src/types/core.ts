// Kiểu dữ liệu của API core: docs/design/api/core.md.

// Một dòng của GET /stock. `avgCost` chỉ có trong phản hồi cho Owner.
export type StockRow = {
  branchId: string;
  skuId: string;
  skuCode: string;
  name: string;
  onHand: number;
  reserved: number;
  available: number;
  avgCost?: number;
  threshold?: number | null;
};

export type StockFilter = {
  branchId?: string;
  skuId?: string;
  query?: string;
  page: number;
  pageSize: number;
};

// `threshold` bỏ trống là bỏ ngưỡng: SKU không còn sinh cảnh báo.
export type SetThresholdRequest = {
  branchId: string;
  skuId: string;
  threshold?: number | null;
};

export type LedgerType = 'IN' | 'OUT';

export type LedgerReason = 'Purchase' | 'Sale' | 'TransferOut' | 'TransferIn' | 'StocktakeAdjust' | 'ReturnIn' | 'Reversal';

export type LedgerReference = 'PurchaseReceipt' | 'Order' | 'StockTransfer' | 'Stocktake';

// Một dòng của GET /ledger. `unitCost` chỉ có trong phản hồi cho Owner.
export type LedgerLine = {
  seq: number;
  id: string;
  branchId: string;
  skuId: string;
  skuCode: string;
  name: string;
  type: LedgerType;
  reason: LedgerReason;
  quantity: number;
  balanceAfter: number;
  unitCost?: number;
  referenceType: LedgerReference;
  referenceId: string;
  reversalOfId?: string | null;
  createdBy?: string | null;
  createdAt: string;
};

// `from` và `to` là thời điểm ISO 8601, tính cả hai đầu.
export type LedgerFilter = {
  branchId?: string;
  skuId?: string;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
};

export type Supplier = {
  id: string;
  name: string;
  phone?: string | null;
  isActive: boolean;
};

export type SupplierRequest = {
  name: string;
  phone?: string;
};

export type PurchaseReceiptStatus = 'Draft' | 'Confirmed';

export type PurchaseReceiptItem = {
  id: string;
  skuId: string;
  skuCode: string;
  skuName: string;
  quantity: number;
  unitCost: number;
};

export type PurchaseReceipt = {
  id: string;
  receiptNumber: string;
  branchId: string;
  supplierId: string;
  status: PurchaseReceiptStatus;
  note?: string | null;
  confirmedAt?: string | null;
  confirmedBy?: string | null;
  createdAt: string;
  items: PurchaseReceiptItem[];
};

export type PurchaseReceiptFilter = {
  branchId?: string;
  status?: PurchaseReceiptStatus;
  page: number;
  pageSize: number;
};

export type PurchaseReceiptLineRequest = {
  skuId: string;
  quantity: number;
  unitCost: number;
};

// Chi nhánh chỉ đặt lúc tạo phiếu.
export type UpdatePurchaseReceiptRequest = {
  supplierId: string;
  note?: string;
  items: PurchaseReceiptLineRequest[];
};

export type CreatePurchaseReceiptRequest = UpdatePurchaseReceiptRequest & { branchId: string };

// Một phần tử `details` của lỗi 409 `insufficient_stock`: docs/design/api/README.md mục "Lỗi".
export type StockShortage = {
  skuId: string;
  requested: number;
  available: number;
};

// Một dòng của GET /pos/skus.
export type PosSku = {
  skuId: string;
  skuCode: string;
  name: string;
  barcodes: string[];
  retailPrice: number;
  available: number;
};

export type OrderChannel = 'POS' | 'Admin' | 'Shopee' | 'TikTok' | 'Lazada';

export type OrderStatus = 'Draft' | 'Reserved' | 'Confirmed' | 'Completed' | 'Cancelled';

export type PaymentMethod = 'Cash' | 'QR';

// `costPrice` chỉ có trong phản hồi cho Owner, sau khi đơn được xác nhận.
export type OrderItem = {
  id: string;
  skuId: string;
  skuCode: string;
  skuName: string;
  quantity: number;
  unitPrice: number;
  discount: number;
  costPrice?: number;
};

export type Payment = {
  method: PaymentMethod;
  amount: number;
  confirmedBy: string;
  confirmedAt: string;
};

// `payment` chỉ có với đơn POS.
export type Order = {
  id: string;
  orderNumber: string;
  branchId: string;
  channel: OrderChannel;
  externalOrderId?: string | null;
  status: OrderStatus;
  totalAmount: number;
  reservedUntil?: string | null;
  confirmedAt?: string | null;
  completedAt?: string | null;
  cancelledAt?: string | null;
  cancelReason?: 'Manual' | 'Expired' | null;
  note?: string | null;
  createdBy?: string | null;
  createdAt: string;
  items: OrderItem[];
  payment?: Payment;
};

// Bỏ trống `unitPrice` thì dùng giá lẻ hiện tại; bỏ trống `discount` là 0.
export type OrderLineRequest = {
  skuId: string;
  quantity: number;
  unitPrice?: number;
  discount?: number;
};

// `payment.amount` là số tiền khách đưa, không nhỏ hơn tổng đơn.
export type PosCheckoutRequest = {
  branchId: string;
  items: OrderLineRequest[];
  payment: { method: PaymentMethod; amount: number };
};
