// Kiểu dữ liệu của API identity: docs/design/api/identity.md.

export type Role = 'Owner' | 'Staff' | 'Cashier';

export type User = {
  id: string;
  fullName: string;
  role: Role;
  branchId?: string | null;
};

export type LoginResponse = {
  accessToken: string;
  expiresIn: number;
  refreshToken: string;
  user: User;
};

export type RegisterTenantRequest = {
  tenantName: string;
  ownerName: string;
  email?: string;
  phone?: string;
  password: string;
};

export type RegisterTenantResponse = {
  tenantId: string;
  userId: string;
};
