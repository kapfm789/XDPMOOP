import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { startSession } from '../auth/session';
import { getGrossProfit, getInventoryValue, getReorderSuggestions, listLowStockAlerts, listTopSellers, runForecast } from './insights';

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const fetchMock = vi.fn<typeof fetch>();

// Lời gọi thứ n đã gửi tới gateway: url và method.
const sent = (n = 0) => {
  const [url, init] = fetchMock.mock.calls[n] ?? [];
  return { url: String(url), method: init?.method, body: init?.body };
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

test('getGrossProfit sends the date range, the filters that are set and the grouping', async () => {
  fetchMock.mockResolvedValueOnce(json(200, {}));

  await getGrossProfit({ from: '2026-11-01', to: '2026-11-07', channel: 'Shopee', skuId: undefined, groupBy: 'day' });

  expect(sent().url).toBe('http://localhost:8080/api/insights/reports/gross-profit?from=2026-11-01&to=2026-11-07&channel=Shopee&groupBy=day');
  expect(sent().method).toBe('GET');
});

test('getInventoryValue sends the page and the branch', async () => {
  fetchMock.mockResolvedValueOnce(json(200, {}));

  await getInventoryValue({ branchId: 'b1', page: 1, pageSize: 50 });

  expect(sent().url).toBe('http://localhost:8080/api/insights/reports/inventory-value?branchId=b1&page=1&pageSize=50');
});

test('listTopSellers with no filter leaves the defaults to the server', async () => {
  fetchMock.mockResolvedValueOnce(json(200, []));

  await listTopSellers({});

  expect(sent().url).toBe('http://localhost:8080/api/insights/reports/top-sellers?');
});

test('listLowStockAlerts adds the branch only when it is given', async () => {
  fetchMock.mockResolvedValue(json(200, []));

  await listLowStockAlerts();
  await listLowStockAlerts('b1');

  expect(sent(0).url).toBe('http://localhost:8080/api/insights/alerts/low-stock?');
  expect(sent(1).url).toBe('http://localhost:8080/api/insights/alerts/low-stock?branchId=b1');
});

test('getReorderSuggestions only reads; it has no body', async () => {
  fetchMock.mockResolvedValueOnce(json(200, {}));

  await getReorderSuggestions({ page: 2, pageSize: 20 });

  expect(sent()).toEqual({ url: 'http://localhost:8080/api/insights/forecast/reorder-suggestions?page=2&pageSize=20', method: 'GET', body: undefined });
});

test('runForecast posts without a body and returns the run id', async () => {
  fetchMock.mockResolvedValueOnce(json(202, { runId: 'r1' }));

  await expect(runForecast()).resolves.toEqual({ runId: 'r1' });

  expect(sent()).toEqual({ url: 'http://localhost:8080/api/insights/forecast/run', method: 'POST', body: undefined });
});
