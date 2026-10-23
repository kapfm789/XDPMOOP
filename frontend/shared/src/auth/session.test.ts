import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { LoginResponse } from '../types/identity';
import { endSession, getAccessToken, getRefreshToken, getSession, startSession, subscribe } from './session';

const login: LoginResponse = {
  accessToken: 'access-1',
  expiresIn: 3600,
  refreshToken: 'refresh-1',
  user: { id: 'u1', fullName: 'Thu ngân', role: 'Cashier', branchId: 'b1' },
};

beforeEach(() => {
  const store = new Map<string, string>();
  vi.stubGlobal('localStorage', {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => void store.set(key, value),
    removeItem: (key: string) => void store.delete(key),
  });
});

afterEach(() => {
  endSession(true);
  vi.unstubAllGlobals();
});

test('keeps the access token in memory and the refresh token in localStorage', () => {
  startSession(login);

  expect(getAccessToken()).toBe('access-1');
  expect(getRefreshToken()).toBe('refresh-1');
  expect(getSession()).toEqual({ user: login.user, ready: true });
});

test('ending the session clears the user and forgets the refresh token only when asked', () => {
  startSession(login);
  endSession(false);
  expect(getAccessToken()).toBeNull();
  expect(getSession()).toEqual({ user: null, ready: true });
  expect(getRefreshToken()).toBe('refresh-1');

  startSession(login);
  endSession(true);
  expect(getRefreshToken()).toBeNull();
});

test('tells subscribers about every change until they unsubscribe', () => {
  const listener = vi.fn();
  const unsubscribe = subscribe(listener);

  startSession(login);
  endSession(true);
  expect(listener).toHaveBeenCalledTimes(2);

  unsubscribe();
  startSession(login);
  expect(listener).toHaveBeenCalledTimes(2);
});
