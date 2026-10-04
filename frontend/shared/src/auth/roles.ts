import type { Role } from '../types/identity';

export const canAccess = (role: Role | undefined, allowed: readonly Role[]) => role !== undefined && allowed.includes(role);

// Lọc menu hoặc route theo vai trò. Chỉ để giao diện gọn; quyền thật do backend kiểm.
export const visibleFor = <T extends { roles: readonly Role[] }>(items: readonly T[], role: Role | undefined) =>
  items.filter((item) => canAccess(role, item.roles));
