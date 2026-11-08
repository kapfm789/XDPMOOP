import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { startSession } from '../auth/session';
import { addBarcode, createBrand, deleteCategory, listProducts, removeBarcode, setPrices, updateCategory } from './catalog';

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

test('listProducts always sends the page and adds only the filters that are set', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { items: [], page: 1, pageSize: 20, total: 0 }));

  await listProducts({ query: '  áo thun ', brandId: 'br1', page: 1, pageSize: 20 });

  expect(sent().url).toBe('http://localhost:8080/api/catalog/products?page=1&pageSize=20&query=%C3%A1o+thun&brandId=br1');
});

test('listProducts leaves out a query that is only spaces', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { items: [], page: 3, pageSize: 10, total: 0 }));

  await listProducts({ query: '  ', page: 3, pageSize: 10 });

  expect(sent().url).toBe('http://localhost:8080/api/catalog/products?page=3&pageSize=10');
});

test('updateCategory puts the new name and parent', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { id: 'c1' }));

  await updateCategory('c1', { name: 'Áo', parentId: 'c0' });

  expect(sent()).toEqual({ url: 'http://localhost:8080/api/catalog/categories/c1', method: 'PUT', body: { name: 'Áo', parentId: 'c0' } });
});

test('deleteCategory sends DELETE and returns nothing on 204', async () => {
  fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

  await expect(deleteCategory('c1')).resolves.toBeUndefined();

  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/catalog/categories/c1', method: 'DELETE' });
});

test('createBrand wraps the name in the body', async () => {
  fetchMock.mockResolvedValueOnce(json(201, { id: 'br1', name: 'Mộc' }));

  await createBrand('Mộc');

  expect(sent()).toEqual({ url: 'http://localhost:8080/api/catalog/brands', method: 'POST', body: { name: 'Mộc' } });
});

test('addBarcode without a code asks the server to generate one', async () => {
  fetchMock.mockResolvedValueOnce(json(201, { id: 'bc1', code: '2000000000015' }));

  await addBarcode('s1', { symbology: 'EAN13' });

  expect(sent()).toEqual({ url: 'http://localhost:8080/api/catalog/skus/s1/barcodes', method: 'POST', body: { symbology: 'EAN13' } });
});

test('removeBarcode targets the barcode under its SKU', async () => {
  fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

  await removeBarcode('s1', 'bc1');

  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/catalog/skus/s1/barcodes/bc1', method: 'DELETE' });
});

test('setPrices puts both prices', async () => {
  fetchMock.mockResolvedValueOnce(json(200, { id: 's1' }));

  await setPrices('s1', { retailPrice: 150000, wholesalePrice: 120000 });

  expect(sent()).toEqual({
    url: 'http://localhost:8080/api/catalog/skus/s1/prices',
    method: 'PUT',
    body: { retailPrice: 150000, wholesalePrice: 120000 },
  });
});
