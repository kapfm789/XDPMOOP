import type { OrderLineRequest, PosSku, StockShortage } from '@oism/shared';

// Một dòng của giỏ. `available` là tồn khả dụng lúc thêm vào giỏ: chỉ để báo sớm, backend mới là nơi quyết định.
export type CartLine = {
  skuId: string;
  skuCode: string;
  name: string;
  unitPrice: number;
  quantity: number;
  available: number;
};

// Thêm một đơn vị của SKU. SKU hết hàng, hoặc dòng đã chạm tồn khả dụng, thì giỏ giữ nguyên (cùng một mảng).
export function addSku(cart: CartLine[], sku: PosSku): CartLine[] {
  const line = cart.find((item) => item.skuId === sku.skuId);
  if ((line?.quantity ?? 0) >= sku.available) return cart;

  if (!line) {
    return [...cart, { skuId: sku.skuId, skuCode: sku.skuCode, name: sku.name, unitPrice: sku.retailPrice, quantity: 1, available: sku.available }];
  }
  return cart.map((item) => (item === line ? { ...item, quantity: item.quantity + 1, available: sku.available } : item));
}

// Số lượng bị chặn ở tồn khả dụng; về 0 thì dòng bị xóa.
export function setQuantity(cart: CartLine[], skuId: string, quantity: number): CartLine[] {
  return cart.flatMap((item) => {
    if (item.skuId !== skuId) return [item];
    const next = Math.min(Math.trunc(quantity), item.available);
    return next > 0 ? [{ ...item, quantity: next }] : [];
  });
}

export const removeLine = (cart: CartLine[], skuId: string) => cart.filter((item) => item.skuId !== skuId);

// Chỉ để hiển thị; số chính thức là `totalAmount` backend trả về.
export const cartTotal = (cart: CartLine[]) => cart.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0);

export const cartCount = (cart: CartLine[]) => cart.reduce((sum, item) => sum + item.quantity, 0);

// Backend trả 409 insufficient_stock: giỏ nhận số còn lại của từng SKU thiếu; SKU đã hết thì rời giỏ.
export function applyShortages(cart: CartLine[], shortages: StockShortage[]): CartLine[] {
  return cart.flatMap((item) => {
    const shortage = shortages.find((entry) => entry.skuId === item.skuId);
    if (!shortage) return [item];
    return shortage.available > 0
      ? [{ ...item, available: shortage.available, quantity: Math.min(item.quantity, shortage.available) }]
      : [];
  });
}

// Mã quét hoặc gõ khớp đúng một mã vạch hoặc mã SKU, không phân biệt hoa thường. Khớp nhiều SKU thì không chọn hộ.
export function exactMatch(skus: PosSku[], text: string): PosSku | undefined {
  const code = text.trim().toLowerCase();
  const matches = skus.filter(
    (sku) => sku.skuCode.toLowerCase() === code || sku.barcodes.some((barcode) => barcode.toLowerCase() === code),
  );
  return matches.length === 1 ? matches[0] : undefined;
}

export const toOrderLines = (cart: CartLine[]): OrderLineRequest[] =>
  cart.map((item) => ({ skuId: item.skuId, quantity: item.quantity, unitPrice: item.unitPrice }));
