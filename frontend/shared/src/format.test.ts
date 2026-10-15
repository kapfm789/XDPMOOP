import { expect, test } from 'vitest';
import { formatDate, formatDateTime, formatMoney, formatNumber, formatPercent, vietnamDate } from './format';

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

test('takes the report date in Vietnam time and shifts it by whole days', () => {
  // 17:30 UTC ngày 20 đã là ngày 21 ở Việt Nam.
  expect(vietnamDate(new Date('2026-10-20T17:30:00Z'))).toBe('2026-10-21');
  expect(vietnamDate(new Date('2026-10-20T16:59:00Z'))).toBe('2026-10-20');
  expect(vietnamDate(new Date('2026-10-20T03:00:00Z'), -29)).toBe('2026-09-21');
  expect(vietnamDate(new Date('2026-12-31T03:00:00Z'), 1)).toBe('2027-01-01');
});

test('formats report dates, quantities and ratios the Vietnamese way', () => {
  expect(formatDate('2026-10-20')).toBe('20/10/2026');
  expect(formatNumber(1234.56, 1)).toBe('1.234,6');
  expect(formatNumber(42)).toBe('42');
  expect(formatPercent(0.1234)).toBe('12,3%');
});
