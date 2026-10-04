import type { Role } from '@oism/shared';

// Cashier không vào được ứng dụng quản trị: docs/design/ui/admin.md.
export const ADMIN_ROLES: readonly Role[] = ['Owner', 'Staff'];
export const OWNER_ONLY: readonly Role[] = ['Owner'];

export type MenuEntry = { path: string; label: string; roles: readonly Role[] };

// Mỗi màn hình mới thêm một dòng ở đây, vai trò theo bảng ở docs/design/ui/admin.md.
export const MENU: readonly MenuEntry[] = [
  { path: '/', label: 'Tổng quan', roles: ADMIN_ROLES },
  { path: '/branches', label: 'Chi nhánh', roles: OWNER_ONLY },
  { path: '/users', label: 'Người dùng', roles: OWNER_ONLY },
  { path: '/catalog/categories', label: 'Danh mục', roles: ADMIN_ROLES },
  { path: '/catalog/brands', label: 'Thương hiệu', roles: ADMIN_ROLES },
  { path: '/catalog/products', label: 'Sản phẩm', roles: ADMIN_ROLES },
];
