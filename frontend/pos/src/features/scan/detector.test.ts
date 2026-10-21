import { expect, test } from 'vitest';
import { freshScan } from './detector';

test('a code staying in frame counts once', () => {
  const seen = new Map<string, number>();
  expect(freshScan(seen, '2000000000015', 0)).toBe(true);
  expect(freshScan(seen, '2000000000015', 200)).toBe(false);
  expect(freshScan(seen, '2000000000015', 1600)).toBe(false);
});

test('a code counts again after leaving the frame for 1.5 seconds', () => {
  const seen = new Map<string, number>();
  freshScan(seen, '2000000000015', 0);
  expect(freshScan(seen, '2000000000015', 1499)).toBe(false);
  expect(freshScan(seen, '2000000000015', 2999)).toBe(true);
});

test('another code counts right away', () => {
  const seen = new Map<string, number>();
  freshScan(seen, '2000000000015', 0);
  expect(freshScan(seen, 'SKU-A1', 100)).toBe(true);
  expect(freshScan(seen, '2000000000015', 200)).toBe(false);
});
