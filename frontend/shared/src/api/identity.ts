import { endSession, getRefreshToken, startSession } from '../auth/session';
import type { Paged } from '../types/common';
import type {
  Branch,
  CreateBranchRequest,
  CreateUserRequest,
  LoginResponse,
  RegisterTenantRequest,
  RegisterTenantResponse,
  UpdateBranchRequest,
  UpdateUserRequest,
  User,
  UserAccount,
} from '../types/identity';
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

export const listBranches = (isActive?: boolean) =>
  api<Branch[]>(`/api/identity/branches${isActive === undefined ? '' : `?isActive=${isActive}`}`);

export const createBranch = (request: CreateBranchRequest) =>
  api<Branch>('/api/identity/branches', { method: 'POST', body: request });

export const updateBranch = (id: string, request: UpdateBranchRequest) =>
  api<Branch>(`/api/identity/branches/${id}`, { method: 'PUT', body: request });

export const setBranchActive = (id: string, isActive: boolean) =>
  api<Branch>(`/api/identity/branches/${id}/active`, { method: 'PATCH', body: { isActive } });

export const listUsers = (page: number, pageSize: number) =>
  api<Paged<UserAccount>>(`/api/identity/users?page=${page}&pageSize=${pageSize}`);

export const createUser = (request: CreateUserRequest) =>
  api<UserAccount>('/api/identity/users', { method: 'POST', body: request });

export const updateUser = (id: string, request: UpdateUserRequest) =>
  api<UserAccount>(`/api/identity/users/${id}`, { method: 'PUT', body: request });
