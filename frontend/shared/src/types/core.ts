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

export type ReservationStatus = 'Active' | 'Consumed' | 'Released';

// Phần giữ hàng của một dòng đơn.
export type Reservation = {
  id: string;
  orderItemId: string;
  skuId: string;
  quantity: number;
  status: ReservationStatus;
  expiresAt: string;
  closedAt?: string | null;
};

// `payment` chỉ có với đơn POS; `reservations` chỉ có trong phản hồi của GET /orders/{id}.
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
  reservations?: Reservation[];
};

// `from` và `to` là thời điểm ISO 8601 lọc theo lúc tạo đơn, tính cả hai đầu.
export type OrderFilter = {
  status?: OrderStatus;
  channel?: OrderChannel;
  branchId?: string;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
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

// Đơn thủ công của kênh Admin: tạo xong là đơn ở Reserved, hàng đã được giữ.
export type CreateOrderRequest = {
  branchId: string;
  note?: string;
  items: OrderLineRequest[];
};

export type TransferStatus = 'Draft' | 'InTransit' | 'Received';

// `unitCost` là giá vốn nơi gửi lúc xuất: chỉ có sau khi xuất và chỉ có trong phản hồi cho Owner.
export type TransferItem = {
  id: string;
  skuId: string;
  skuCode: string;
  skuName: string;
  quantity: number;
  unitCost?: number;
};

export type StockTransfer = {
  id: string;
  transferNumber: string;
  fromBranchId: string;
  toBranchId: string;
  status: TransferStatus;
  shippedAt?: string | null;
  receivedAt?: string | null;
  createdBy?: string | null;
  items: TransferItem[];
};

export type TransferFilter = {
  status?: TransferStatus;
  page: number;
  pageSize: number;
};

export type CreateTransferRequest = {
  fromBranchId: string;
  toBranchId: string;
  items: { skuId: string; quantity: number }[];
};

export type StocktakeStatus = 'Open' | 'Posted';

// `systemQty` là `onHand` lúc chốt và `difference` bằng `countedQty - systemQty`; cả hai là null khi phiên còn Open.
export type StocktakeItem = {
  id: string;
  skuId: string;
  skuCode: string;
  skuName: string;
  countedQty: number;
  systemQty?: number | null;
  difference?: number | null;
};

export type Stocktake = {
  id: string;
  branchId: string;
  status: StocktakeStatus;
  createdBy?: string | null;
  postedAt?: string | null;
  items: StocktakeItem[];
};

export type StocktakeFilter = {
  branchId?: string;
  status?: StocktakeStatus;
  page: number;
  pageSize: number;
};

// Thay cả bộ số đếm của phiên.
export type StocktakeCount = {
  skuId: string;
  countedQty: number;
};

// Một phần tử `details` của lỗi 409 `stocktake_below_reserved`: SKU đếm thấp hơn lượng đang giữ, kèm các đơn đang giữ nó.
export type StocktakeShortfall = {
  skuId: string;
  countedQty: number;
  reserved: number;
  orders: { orderId: string; orderNumber: string; channel: OrderChannel; quantity: number }[];
};
