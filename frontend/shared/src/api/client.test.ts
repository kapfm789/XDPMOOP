import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { endSession, getRefreshToken, getSession, startSession } from '../auth/session';
import type { LoginResponse } from '../types/identity';
import { api, ApiError } from './client';

const user = { id: 'u1', fullName: 'Chủ cửa hàng', role: 'Owner' as const };
const session = (n: number): LoginResponse => ({ accessToken: `access-${n}`, expiresIn: 3600, refreshToken: `refresh-${n}`, user });

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
const problem = (status: number, code: string, extra: object = {}) => json(status, { status, code, title: code, ...extra });

const fetchMock = vi.fn<typeof fetch>();
const calls = () => fetchMock.mock.calls.map(([url, init]) => ({
  url: String(url),
  authorization: (init?.headers as Record<string, string>).Authorization,
}));

beforeEach(() => {
  const store = new Map<string, string>();
  vi.stubGlobal('localStorage', {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => void store.set(key, value),
    removeItem: (key: string) => void store.delete(key),
  });
  vi.stubGlobal('fetch', fetchMock);
  startSession(session(1));
});

afterEach(() => {
  fetchMock.mockReset();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

test('sends the access token to the gateway and returns the body', async () => {
  fetchMock.mockResolvedValueOnce(json(200, [{ id: 'b1' }]));

  const result = await api<{ id: string }[]>('/api/catalog/brands');

  expect(result).toEqual([{ id: 'b1' }]);
  expect(calls()).toEqual([{ url: 'http://localhost:8080/api/catalog/brands', authorization: 'Bearer access-1' }]);
});

test('on 401 refreshes once and repeats the request with the new token', async () => {
  fetchMock
    .mockResolvedValueOnce(problem(401, 'unauthenticated'))
    .mockResolvedValueOnce(json(200, session(2)))
    .mockResolvedValueOnce(json(200, { ok: true }));

  await expect(api('/api/catalog/brands')).resolves.toEqual({ ok: true });

  expect(calls()).toEqual([
    { url: 'http://localhost:8080/api/catalog/brands', authorization: 'Bearer access-1' },
    { url: 'http://localhost:8080/api/identity/auth/refresh', authorization: undefined },
    { url: 'http://localhost:8080/api/catalog/brands', authorization: 'Bearer access-2' },
  ]);
  expect(JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body))).toEqual({ refreshToken: 'refresh-1' });
  expect(getRefreshToken()).toBe('refresh-2');
});

test('requests that hit 401 together share one refresh', async () => {
  fetchMock.mockImplementation(async (url, init) => {
    if (String(url).endsWith('/auth/refresh')) return json(200, session(2));
    const authorization = (init?.headers as Record<string, string>).Authorization;
    return authorization === 'Bearer access-2' ? json(200, { ok: true }) : problem(401, 'unauthenticated');
  });

  await Promise.all([api('/api/catalog/brands'), api('/api/catalog/categories')]);

  expect(calls().filter((call) => call.url.endsWith('/auth/refresh'))).toHaveLength(1);
});

test('when the refresh token is rejected the session ends and the 401 surfaces', async () => {
  fetchMock
    .mockResolvedValueOnce(problem(401, 'unauthenticated'))
    .mockResolvedValueOnce(problem(401, 'unauthenticated'));

  await expect(api('/api/catalog/brands')).rejects.toMatchObject({ status: 401, code: 'unauthenticated' });

  expect(fetchMock).toHaveBeenCalledTimes(2);
  expect(getSession()).toEqual({ user: null, ready: true });
  expect(getRefreshToken()).toBeNull();
});

test('anonymous calls carry no token and do not refresh on 401', async () => {
  fetchMock.mockResolvedValueOnce(problem(401, 'unauthenticated'));

  await expect(api('/api/identity/auth/login', { method: 'POST', body: {}, anonymous: true })).rejects.toBeInstanceOf(ApiError);

  expect(calls()).toEqual([{ url: 'http://localhost:8080/api/identity/auth/login', authorization: undefined }]);
  expect(getSession().user).toEqual(user);
});

test('turns ProblemDetails into ApiError with code, field errors and details', async () => {
  fetchMock.mockResolvedValueOnce(
    problem(400, 'validation_failed', { title: 'Dữ liệu không hợp lệ', errors: { name: ['Bắt buộc'] }, details: [1] }),
  );

  const error = await api('/api/catalog/brands', { method: 'POST', body: { name: '' } }).catch((e: unknown) => e);

  expect(error).toBeInstanceOf(ApiError);
  expect(error).toMatchObject({ status: 400, code: 'validation_failed', message: 'Dữ liệu không hợp lệ', errors: { name: ['Bắt buộc'] }, details: [1] });
});

test('returns undefined for 204', async () => {
  fetchMock.mockResolvedValueOnce(json(204));

  await expect(api<void>('/api/catalog/categories/c1', { method: 'DELETE' })).resolves.toBeUndefined();
});

test('retries reference_not_ready one second apart until it succeeds', async () => {
  vi.useFakeTimers();
  fetchMock
    .mockResolvedValueOnce(problem(409, 'reference_not_ready'))
    .mockResolvedValueOnce(problem(409, 'reference_not_ready'))
    .mockResolvedValueOnce(json(201, { id: 'o1' }));

  const result = api('/api/core/orders', { method: 'POST', body: {} });
  await vi.advanceTimersByTimeAsync(999);
  expect(fetchMock).toHaveBeenCalledTimes(1);
  await vi.advanceTimersByTimeAsync(1001);

  await expect(result).resolves.toEqual({ id: 'o1' });
  expect(fetchMock).toHaveBeenCalledTimes(3);
});

test('gives up on reference_not_ready after three retries', async () => {
  vi.useFakeTimers();
  fetchMock.mockImplementation(async () => problem(409, 'reference_not_ready'));

  const result = api('/api/core/orders', { method: 'POST', body: {} }).catch((e: unknown) => e);
  await vi.advanceTimersByTimeAsync(3000);

  expect(await result).toMatchObject({ status: 409, code: 'reference_not_ready' });
  expect(fetchMock).toHaveBeenCalledTimes(4);
});

test('other conflicts are not retried', async () => {
  fetchMock.mockResolvedValueOnce(problem(409, 'duplicate'));

  await expect(api('/api/catalog/brands', { method: 'POST', body: { name: 'Nike' } })).rejects.toMatchObject({ code: 'duplicate' });

  expect(fetchMock).toHaveBeenCalledTimes(1);
  endSession(true);
});
