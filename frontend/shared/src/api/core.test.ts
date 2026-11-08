import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { startSession } from '../auth/session';
import {
  deleteTransfer,
  listLedger,
  listStock,
  openStocktake,
  posCheckout,
  searchPosSkus,
  setStockThreshold,
  setStocktakeCounts,
  transitionOrder,
  transitionTransfer,
} from './core';

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const fetchMock = vi.fn<typeof fetch>();

// Lời gọi thứ n đã gửi tới gateway: url, method, header và body đã giải mã.
const sent = (n = 0) => {
  const [url, init] = fetchMock.mock.calls[n] ?? [];
  return {
    url: String(url),
    method: init?.method,
    headers: init?.headers as Record<string, string>,
    body: init?.body === undefined ? undefined : JSON.parse(String(init.body)),
  };
};

beforeEach(() => {
  const store = new Map<string, string>();
  vi.stubGlobal('localStorage', {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => void store.set(key, value),
    removeItem: (key: string) => void store.delete(key),
  });
  vi.stubGlobal('fetch', fetchMock);
  startSession({ accessToken: 'access', expiresIn: 3600, refreshToken: 'refresh', user: { id: 'u1', fullName: 'Chủ cửa hàng', role: 'Owner' } });
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
});

test('listStock drops empty filters and trims the search text', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { items: [], page: 1, pageSize: 20, total: 0 }));

  await listStock({ branchId: 'b1', skuId: undefined, query: '  áo  ', page: 1, pageSize: 20 });

  expect(sent().url).toBe('http://localhost:8080/api/core/stock?branchId=b1&page=1&pageSize=20&query=%C3%A1o');
  expect(sent().method).toBe('GET');
});

test('listStock leaves out a query that is only spaces', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { items: [], page: 1, pageSize: 20, total: 0 }));

  await listStock({ query: '   ', page: 2, pageSize: 50 });

  expect(sent().url).toBe('http://localhost:8080/api/core/stock?page=2&pageSize=50');
});

test('listLedger sends only the filters that have a value', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { items: [], page: 1, pageSize: 20, total: 0 }));

  await listLedger({ skuId: 's1', from: '2026-11-01', to: '', page: 1, pageSize: 20 });

  expect(sent().url).toBe('http://localhost:8080/api/core/ledger?skuId=s1&from=2026-11-01&page=1&pageSize=20');
});

test('setStockThreshold puts the request as the body', async () => {
  fetchMock.mockResolvedValueOnce(json(200, {}));

  await setStockThreshold({ branchId: 'b1', skuId: 's1', threshold: null });

  expect(sent()).toMatchObject({
    url: 'http://localhost:8080/api/core/stock/threshold',
    method: 'PUT',
    body: { branchId: 'b1', skuId: 's1', threshold: null },
  });
});

test('searchPosSkus sends the branch and the trimmed search text', async () => {
  fetchMock.mockResolvedValueOnce(json(200, []));

  await searchPosSkus('b1', '  8930001  ');

  expect(sent().url).toBe('http://localhost:8080/api/core/pos/skus?branchId=b1&query=8930001');
});

test('posCheckout posts the cart with the Idempotency-Key header', async () => {
  const order = { id: 'o1', status: 'Completed' };
  fetchMock.mockResolvedValueOnce(json(201, order));
  const request = { branchId: 'b1', items: [{ skuId: 's1', quantity: 2 }], payment: { method: 'Cash' as const, amount: 300000 } };

  const result = await posCheckout(request, 'key-1');

  expect(result).toEqual(order);
  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/core/pos/checkout', method: 'POST', body: request });
  expect(sent().headers['Idempotency-Key']).toBe('key-1');
  expect(sent().headers.Authorization).toBe('Bearer access');
});

test('posCheckout resent with the same key carries the same key', async () => {
  fetchMock.mockResolvedValue(json(200, { id: 'o1' }));
  const request = { branchId: 'b1', items: [{ skuId: 's1', quantity: 1 }], payment: { method: 'QR' as const, amount: 150000 } };

  await posCheckout(request, 'key-1');
  await posCheckout(request, 'key-1');

  expect(sent(0).headers['Idempotency-Key']).toBe('key-1');
  expect(sent(1).headers['Idempotency-Key']).toBe('key-1');
});

test.each(['confirm', 'complete', 'cancel'] as const)('transitionOrder posts to the %s action of the order', async (action) => {
  fetchMock.mockResolvedValueOnce(json(200, { id: 'o1' }));

  await transitionOrder('o1', action);

  expect(sent()).toMatchObject({ url: `http://localhost:8080/api/core/orders/o1/${action}`, method: 'POST' });
  expect(sent().body).toBeUndefined();
});

test.each(['ship', 'receive'] as const)('transitionTransfer posts to the %s action of the transfer', async (action) => {
  fetchMock.mockResolvedValueOnce(json(200, { id: 't1' }));

  await transitionTransfer('t1', action);

  expect(sent()).toMatchObject({ url: `http://localhost:8080/api/core/transfers/t1/${action}`, method: 'POST' });
});

test('deleteTransfer sends DELETE and returns nothing on 204', async () => {
  fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

  await expect(deleteTransfer('t1')).resolves.toBeUndefined();

  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/core/transfers/t1', method: 'DELETE' });
});

test('openStocktake posts the branch in the body', async () => {
  fetchMock.mockResolvedValueOnce(json(201, { id: 'st1' }));

  await openStocktake('b1');

  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/core/stocktakes', method: 'POST', body: { branchId: 'b1' } });
});

test('setStocktakeCounts wraps the counts in an items field', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { id: 'st1' }));
  const items = [{ skuId: 's1', countedQty: 6 }, { skuId: 's2', countedQty: 0 }];

  await setStocktakeCounts('st1', items);

  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/core/stocktakes/st1/counts', method: 'PUT', body: { items } });
});
