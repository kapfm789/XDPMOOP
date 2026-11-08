import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { getAccessToken, getRefreshToken, getSession, startSession } from '../auth/session';
import type { LoginResponse } from '../types/identity';
import { listBranches, listUsers, login, logout, registerTenant, setBranchActive } from './identity';

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const user = { id: 'u1', fullName: 'Chủ cửa hàng', role: 'Owner' as const };
const loginResponse: LoginResponse = { accessToken: 'access-1', expiresIn: 3600, refreshToken: 'refresh-1', user };

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
});

afterEach(() => {
  fetchMock.mockReset();
  vi.unstubAllGlobals();
});

test('registerTenant is sent without a token', async () => {
  startSession(loginResponse);
  fetchMock.mockResolvedValueOnce(json(201, { tenantId: 't1', userId: 'u1' }));
  const request = { tenantName: 'Cửa hàng Mộc', ownerName: 'Nguyễn Văn A', email: 'owner@moc.test', password: 'matkhau123' };

  await registerTenant(request);

  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/identity/tenants', method: 'POST', body: request });
  expect(sent().headers.Authorization).toBeUndefined();
});

test('login posts the identifier, starts the session and returns the user', async () => {
  fetchMock.mockResolvedValueOnce(json(200, loginResponse));

  const result = await login('owner@moc.test', 'matkhau123');

  expect(result).toEqual(user);
  expect(sent()).toMatchObject({
    url: 'http://localhost:8080/api/identity/auth/login',
    method: 'POST',
    body: { identifier: 'owner@moc.test', password: 'matkhau123' },
  });
  expect(sent().headers.Authorization).toBeUndefined();
  expect(getAccessToken()).toBe('access-1');
  expect(getRefreshToken()).toBe('refresh-1');
  expect(getSession().user).toEqual(user);
});

test('logout revokes the refresh token on the server and forgets the session', async () => {
  startSession(loginResponse);
  fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

  await logout();

  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/identity/auth/logout', method: 'POST', body: { refreshToken: 'refresh-1' } });
  expect(getRefreshToken()).toBeNull();
  expect(getSession().user).toBeNull();
});

test('logout still forgets the session when the network fails', async () => {
  startSession(loginResponse);
  fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'));

  await expect(logout()).rejects.toThrow('Failed to fetch');

  expect(getRefreshToken()).toBeNull();
  expect(getSession().user).toBeNull();
});

test('logout without a refresh token calls nothing', async () => {
  await logout();

  expect(fetchMock).not.toHaveBeenCalled();
});

test('listBranches adds isActive only when it is given', async () => {
  startSession(loginResponse);
  fetchMock.mockResolvedValue(json(200, []));

  await listBranches();
  await listBranches(false);

  expect(sent(0).url).toBe('http://localhost:8080/api/identity/branches');
  expect(sent(1).url).toBe('http://localhost:8080/api/identity/branches?isActive=false');
});

test('setBranchActive patches the active flag', async () => {
  startSession(loginResponse);
  fetchMock.mockResolvedValueOnce(json(200, { id: 'b1' }));

  await setBranchActive('b1', false);

  expect(sent()).toMatchObject({ url: 'http://localhost:8080/api/identity/branches/b1/active', method: 'PATCH', body: { isActive: false } });
});

test('listUsers sends the page in the query', async () => {
  startSession(loginResponse);
  fetchMock.mockResolvedValueOnce(json(200, { items: [], page: 2, pageSize: 10, total: 0 }));

  await listUsers(2, 10);

  expect(sent().url).toBe('http://localhost:8080/api/identity/users?page=2&pageSize=10');
});
