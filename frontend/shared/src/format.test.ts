import { expect, test } from 'vitest';
import { formatDateTime, formatMoney } from './format';

test('formats money the Vietnamese way without decimals', () => {
  // Intl đặt một khoảng trắng không ngắt trước ký hiệu ₫.
  expect(formatMoney(150000).replace(/\s/g, ' ')).toBe('150.000 ₫');
  expect(formatMoney(0).replace(/\s/g, ' ')).toBe('0 ₫');
  expect(formatMoney(5500.5).replace(/\s/g, ' ')).toBe('5.501 ₫');
});

test('shows a UTC time from the API in Vietnam time', () => {
  // 03:15 UTC là 10:15 ở Việt Nam; 18:30 UTC đã sang ngày hôm sau.
  expect(formatDateTime('2026-10-20T03:15:27Z')).toBe('10:15 20/10/2026');
  expect(formatDateTime('2026-10-20T18:30:00Z')).toBe('01:30 21/10/2026');
});
