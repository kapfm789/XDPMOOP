import { endSession, getRefreshToken, startSession } from '../auth/session';
import type { LoginResponse, RegisterTenantRequest, RegisterTenantResponse, User } from '../types/identity';
import { api } from './client';

export const registerTenant = (request: RegisterTenantRequest) =>
  api<RegisterTenantResponse>('/api/identity/tenants', { method: 'POST', body: request, anonymous: true });

export async function login(identifier: string, password: string): Promise<User> {
  const response = await api<LoginResponse>('/api/identity/auth/login', {
    method: 'POST',
    body: { identifier, password },
    anonymous: true,
  });
  startSession(response);
  return response.user;
}

export async function logout() {
  const refreshToken = getRefreshToken();
  try {
    if (refreshToken) await api<void>('/api/identity/auth/logout', { method: 'POST', body: { refreshToken } });
  } finally {
    // Thu hồi lỗi (mất mạng) thì phía trình duyệt vẫn phải quên phiên.
    endSession(true);
  }
}
