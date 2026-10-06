import type { PosSku } from '@oism/shared';
import { expect, test } from 'vitest';
import { addSku, applyShortages, cartCount, cartTotal, exactMatch, removeLine, setQuantity, toOrderLines } from './cart';

const shirt: PosSku = { skuId: 's1', skuCode: 'AO-01', name: 'Áo thun', barcodes: ['8930000000017'], retailPrice: 150000, available: 2 };
const trousers: PosSku = { skuId: 's2', skuCode: 'QUAN-01', name: 'Quần kaki', barcodes: ['8930000000024'], retailPrice: 80000, available: 5 };
const soldOut: PosSku = { ...trousers, skuId: 's3', skuCode: 'MU-01', barcodes: [], available: 0 };

test('adding a SKU starts at one and stops at the available stock (UC-POS-01 AC-2, AC-3, AC-4)', () => {
  const one = addSku([], shirt);
  const two = addSku(one, shirt);

  expect(one).toEqual([{ skuId: 's1', skuCode: 'AO-01', name: 'Áo thun', unitPrice: 150000, quantity: 1, available: 2 }]);
  expect(two[0].quantity).toBe(2);
  // Đã bằng tồn khả dụng: giỏ giữ nguyên, để giao diện biết mà báo số còn lại.
  expect(addSku(two, shirt)).toBe(two);
  // SKU hết hàng không thêm được vào giỏ.
  expect(addSku(two, soldOut)).toBe(two);
});

test('totals, quantity changes and removal', () => {
  const cart = addSku(addSku(addSku([], shirt), shirt), trousers);

  expect(cartTotal(cart)).toBe(380000);
  expect(cartCount(cart)).toBe(3);
  expect(setQuantity(cart, 's2', 9)[1].quantity).toBe(5);
  expect(setQuantity(cart, 's2', 0).map((line) => line.skuId)).toEqual(['s1']);
  expect(removeLine(cart, 's1').map((line) => line.skuId)).toEqual(['s2']);
  expect(toOrderLines(cart)).toEqual([
    { skuId: 's1', quantity: 2, unitPrice: 150000 },
    { skuId: 's2', quantity: 1, unitPrice: 80000 },
  ]);
});

test('a 409 insufficient_stock updates what is left and drops sold-out lines', () => {
  const cart = setQuantity(addSku(addSku(addSku([], shirt), shirt), trousers), 's2', 4);

  const next = applyShortages(cart, [
    { skuId: 's1', requested: 2, available: 0 },
    { skuId: 's2', requested: 4, available: 3 },
  ]);

  expect(next).toEqual([{ skuId: 's2', skuCode: 'QUAN-01', name: 'Quần kaki', unitPrice: 80000, quantity: 3, available: 3 }]);
});

test('a scanned code picks a SKU only when it matches exactly one barcode or SKU code', () => {
  const skus = [shirt, trousers];

  expect(exactMatch(skus, ' 8930000000024 ')).toBe(trousers);
  expect(exactMatch(skus, 'ao-01')).toBe(shirt);
  expect(exactMatch(skus, 'AO')).toBeUndefined();
  expect(exactMatch([shirt, { ...trousers, barcodes: ['8930000000017'] }], '8930000000017')).toBeUndefined();
});
