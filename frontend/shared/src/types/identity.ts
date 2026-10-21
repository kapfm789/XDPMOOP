// Kiểu dữ liệu của API identity: docs/design/api/identity.md.

export type Role = 'Owner' | 'Staff' | 'Cashier';

export type User = {
  id: string;
  fullName: string;
  role: Role;
  branchId?: string | null;
};

// GET /api/identity/me: người đang đăng nhập kèm tên cửa hàng.
export type Me = User & { tenantName: string };

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

export type BranchType = 'Store' | 'Warehouse';

export type Branch = {
  id: string;
  code: string;
  name: string;
  type: BranchType;
  address?: string | null;
  // Tài khoản nhận chuyển khoản cho mã VietQR ở POS; có cả hai hoặc cùng null (W5-12).
  bankBin?: string | null;
  bankAccount?: string | null;
  isActive: boolean;
};

export type CreateBranchRequest = {
  code: string;
  name: string;
  type: BranchType;
  address?: string;
  bankBin?: string;
  bankAccount?: string;
};

// Mã chi nhánh chỉ đặt lúc tạo.
export type UpdateBranchRequest = Omit<CreateBranchRequest, 'code'>;

// Người dùng trong màn hình quản lý; khác `User` là người đang đăng nhập.
export type UserAccount = {
  id: string;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  role: Role;
  branchId?: string | null;
  isActive: boolean;
  createdAt: string;
};

// Owner chỉ sinh ra khi đăng ký tenant; API người dùng chỉ nhận hai vai trò này.
export type AssignableRole = Exclude<Role, 'Owner'>;

export type CreateUserRequest = {
  fullName: string;
  email?: string;
  phone?: string;
  password: string;
  role: AssignableRole;
  branchId?: string | null;
};

export type UpdateUserRequest = {
  fullName: string;
  role: AssignableRole;
  branchId?: string | null;
  isActive: boolean;
};
