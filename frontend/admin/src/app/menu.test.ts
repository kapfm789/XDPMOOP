import { expect, test } from 'vitest';
import { findEntry, menuFor } from './menu';

test('a sub-page lights up the menu entry it belongs to', () => {
  expect(findEntry('/catalog/products/0f9e8d7c')?.entry.path).toBe('/catalog/products');
  expect(findEntry('/orders/0f9e8d7c')?.entry.path).toBe('/orders');
  expect(findEntry('/')?.entry.path).toBe('/');
});

test('the longest entry wins and paths compare by whole segments', () => {
  expect(findEntry('/orders/new')?.entry.path).toBe('/orders/new');
  expect(findEntry('/inventory/stocktakes')?.entry.path).toBe('/inventory/stocktakes');
  expect(findEntry('/inventory/stock')?.group.key).toBe('inventory');
});

test('an unknown path has no entry', () => {
  expect(findEntry('/nowhere')).toBeUndefined();
});

test('staff sees neither owner-only entries nor the settings group', () => {
  const staff = menuFor('Staff');
  expect(staff.map((group) => group.key)).toEqual(['home', 'catalog', 'inventory', 'orders', 'reports']);
  expect(staff.find((group) => group.key === 'reports')?.entries.map((entry) => entry.path)).toEqual(['/reports/inventory']);
});

test('owner sees every group', () => {
  expect(menuFor('Owner').map((group) => group.key)).toEqual([
    'home',
    'catalog',
    'inventory',
    'orders',
    'reports',
    'channel',
    'settings',
  ]);
});
