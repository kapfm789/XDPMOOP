import { expect, test } from 'vitest';
import type { Role } from '../types/identity';
import { canAccess, visibleFor } from './roles';

const menu: { key: string; roles: Role[] }[] = [
  { key: 'home', roles: ['Owner', 'Staff'] },
  { key: 'branches', roles: ['Owner'] },
  { key: 'pos', roles: ['Owner', 'Cashier'] },
];

test('visibleFor keeps only the items the role may open', () => {
  expect(visibleFor(menu, 'Owner').map((item) => item.key)).toEqual(['home', 'branches', 'pos']);
  expect(visibleFor(menu, 'Staff').map((item) => item.key)).toEqual(['home']);
  expect(visibleFor(menu, 'Cashier').map((item) => item.key)).toEqual(['pos']);
});

test('nobody signed in sees nothing', () => {
  expect(visibleFor(menu, undefined)).toEqual([]);
  expect(canAccess(undefined, ['Owner'])).toBe(false);
});
