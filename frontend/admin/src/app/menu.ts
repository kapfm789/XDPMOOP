import type { Role } from '@oism/shared';

// Cashier không vào được ứng dụng quản trị: docs/design/ui/admin.md.
export const ADMIN_ROLES: readonly Role[] = ['Owner', 'Staff'];

export type MenuEntry = { path: string; label: string; roles: readonly Role[] };

// Mỗi màn hình mới thêm một dòng ở đây, vai trò theo bảng ở docs/design/ui/admin.md.
export const MENU: readonly MenuEntry[] = [{ path: '/', label: 'Tổng quan', roles: ADMIN_ROLES }];
