import { expect, test } from 'vitest';
import { expiresWithin, retryDelay } from './connection';

const NOW = Date.UTC(2026, 9, 20, 3, 0, 0);

// JWT không ký, chỉ cần phần payload có exp tính bằng giây.
function token(payload: object): string {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${encode({ alg: 'RS256' })}.${encode(payload)}.signature`;
}

test('reconnects forever, backing off to 30 seconds', () => {
  expect([0, 1, 2, 3, 4, 50].map(retryDelay)).toEqual([0, 2000, 5000, 10000, 30000, 30000]);
});

test('treats a token as expiring when exp falls inside the margin', () => {
  expect(expiresWithin(token({ exp: NOW / 1000 + 3600 }), 30_000, NOW)).toBe(false);
  expect(expiresWithin(token({ exp: NOW / 1000 + 10 }), 30_000, NOW)).toBe(true);
  expect(expiresWithin(token({ exp: NOW / 1000 - 1 }), 30_000, NOW)).toBe(true);
});

test('treats an unreadable token as expired so the session is refreshed', () => {
  expect(expiresWithin('not-a-jwt', 30_000, NOW)).toBe(true);
  expect(expiresWithin(token({ sub: 'no-exp' }), 30_000, NOW)).toBe(true);
});
