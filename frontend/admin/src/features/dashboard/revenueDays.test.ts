import type { GrossProfitRow } from '@oism/shared';
import { expect, test } from 'vitest';
import { axisMax, fillDays } from './revenueDays';

const row = (key: string, revenue: number): GrossProfitRow => ({
  key,
  revenue,
  cogs: 0,
  grossProfit: revenue,
  quantity: 1,
  skuCode: null,
  name: null,
});

test('days without sales show zero revenue', () => {
  expect(fillDays([row('2026-10-14', 300000), row('2026-10-16', 150000)], '2026-10-14', 4)).toEqual([
    { date: '2026-10-14', revenue: 300000 },
    { date: '2026-10-15', revenue: 0 },
    { date: '2026-10-16', revenue: 150000 },
    { date: '2026-10-17', revenue: 0 },
  ]);
});

test('the window crosses month ends', () => {
  expect(fillDays([], '2026-10-30', 3).map((day) => day.date)).toEqual(['2026-10-30', '2026-10-31', '2026-11-01']);
});

test('the axis rounds up to a readable number', () => {
  expect(axisMax([1_230_000, 400_000])).toBe(2_000_000);
  expect(axisMax([950_000])).toBe(1_000_000);
  expect(axisMax([0, 0])).toBe(1);
});
