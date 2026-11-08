import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { startSession } from '../auth/session';
import { createChannelShop, listWebhookEvents, setChannelShopActive, simulateBurst } from './channel';

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const fetchMock = vi.fn<typeof fetch>();

// Lời gọi thứ n đã gửi tới gateway: url, method và body đã giải mã.
const sent = (n = 0) => {
  const [url, init] = fetchMock.mock.calls[n] ?? [];
  return {
    url: String(url),
    method: init?.method,
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

test('createChannelShop posts the shop and returns its secret', async () => {
  const shop = { id: 'cs1', channel: 'Shopee', shopId: 'shop-1', branchId: 'b1', isActive: true, secret: 's3cret' };
  fetchMock.mockResolvedValueOnce(json(201, shop));

  await expect(createChannelShop({ channel: 'Shopee', shopId: 'shop-1', branchId: 'b1' })).resolves.toEqual(shop);

  expect(sent()).toEqual({
    url: 'http://localhost:8080/api/channel/channel-shops',
    method: 'POST',
    body: { channel: 'Shopee', shopId: 'shop-1', branchId: 'b1' },
  });
});

test('setChannelShopActive patches the active flag', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { id: 'cs1' }));

  await setChannelShopActive('cs1', false);

  expect(sent()).toEqual({ url: 'http://localhost:8080/api/channel/channel-shops/cs1/active', method: 'PATCH', body: { isActive: false } });
});

test('listWebhookEvents always sends the page and adds channel and status only when set', async () => {
  fetchMock.mockResolvedValue(json(200, { items: [], page: 1, pageSize: 20, total: 0 }));

  await listWebhookEvents({ page: 1, pageSize: 20 });
  await listWebhookEvents({ channel: 'TikTok', status: 'Rejected', page: 2, pageSize: 20 });

  expect(sent(0).url).toBe('http://localhost:8080/api/channel/webhook-events?page=1&pageSize=20');
  expect(sent(1).url).toBe('http://localhost:8080/api/channel/webhook-events?page=2&pageSize=20&channel=TikTok&status=Rejected');
});

test('simulateBurst posts the burst request as the body', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { burstId: 'bu1', sent: 50 }));
  const request = { channel: 'Lazada' as const, shopId: 'shop-1', skuCode: 'AT-A-DEN-M', quantityPerOrder: 1, orders: 50, concurrency: 50 };

  await simulateBurst(request);

  expect(sent()).toEqual({ url: 'http://localhost:8080/api/channel/simulator/burst', method: 'POST', body: request });
});
