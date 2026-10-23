import { expect, test } from 'vitest';
import { skuLabel } from './skuLabel';

test('shows the SKU code and name once insights knows the SKU', () => {
  expect(skuLabel({ skuId: 's1', skuCode: 'PK-TL', name: 'Thắt lưng da' })).toBe('PK-TL · Thắt lưng da');
});

test('shows the SKU id while SkuUpserted has not reached insights yet', () => {
  expect(skuLabel({ skuId: 's1', skuCode: null, name: null })).toBe('s1');
});
