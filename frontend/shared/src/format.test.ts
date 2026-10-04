import { expect, test } from 'vitest';
import { formatMoney } from './format';

test('formats money the Vietnamese way without decimals', () => {
  // Intl đặt một khoảng trắng không ngắt trước ký hiệu ₫.
  expect(formatMoney(150000).replace(/\s/g, ' ')).toBe('150.000 ₫');
  expect(formatMoney(0).replace(/\s/g, ' ')).toBe('0 ₫');
  expect(formatMoney(5500.5).replace(/\s/g, ' ')).toBe('5.501 ₫');
});
