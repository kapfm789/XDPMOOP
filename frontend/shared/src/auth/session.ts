import type { LoginResponse, User } from '../types/identity';

// Access token chỉ nằm trong bộ nhớ. Refresh token nằm ở localStorage để mở lại phiên sau khi tải lại trang.
const REFRESH_TOKEN_KEY = 'oism.refreshToken';

// ready là false cho tới khi biết phiên cũ có mở lại được hay không.
export type Session = { user: User | null; ready: boolean };

let accessToken: string | null = null;
let session: Session = { user: null, ready: false };
const listeners = new Set<() => void>();

export const getAccessToken = () => accessToken;
export const getRefreshToken = () => localStorage.getItem(REFRESH_TOKEN_KEY);
export const getSession = () => session;

export function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function startSession(login: LoginResponse) {
  accessToken = login.accessToken;
  localStorage.setItem(REFRESH_TOKEN_KEY, login.refreshToken);
  publish({ user: login.user, ready: true });
}

export function endSession(forgetRefreshToken: boolean) {
  accessToken = null;
  if (forgetRefreshToken) localStorage.removeItem(REFRESH_TOKEN_KEY);
  publish({ user: null, ready: true });
}

function publish(next: Session) {
  session = next;
  listeners.forEach((listener) => listener());
}
