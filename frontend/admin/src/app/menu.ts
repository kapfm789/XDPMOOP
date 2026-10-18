import { type Role, visibleFor } from '@oism/shared';
import type { IconName } from '../features/common/icons';

// Cashier không vào được ứng dụng quản trị: docs/design/ui/admin.md.
export const ADMIN_ROLES: readonly Role[] = ['Owner', 'Staff'];
export const OWNER_ONLY: readonly Role[] = ['Owner'];

export type MenuEntry = { path: string; label: string; roles: readonly Role[] };

// Nhóm của thanh bên. Nhóm chỉ có một mục thì thanh bên hiện thẳng mục đó, không có cấp con.
export type MenuGroup = { key: string; label: string; icon: IconName; entries: readonly MenuEntry[] };

// Mỗi màn hình mới thêm một dòng ở đây; nhóm theo mục "Bố cục và phong cách", vai trò theo bảng màn hình ở
// docs/design/ui/admin.md.
export const MENU: readonly MenuGroup[] = [
  { key: 'home', label: 'Tổng quan', icon: 'dashboard', entries: [{ path: '/', label: 'Tổng quan', roles: ADMIN_ROLES }] },
  {
    key: 'catalog',
    label: 'Sản phẩm',
    icon: 'product',
    entries: [
      { path: '/catalog/categories', label: 'Danh mục', roles: ADMIN_ROLES },
      { path: '/catalog/brands', label: 'Thương hiệu', roles: ADMIN_ROLES },
      { path: '/catalog/products', label: 'Sản phẩm', roles: ADMIN_ROLES },
    ],
  },
  {
    key: 'inventory',
    label: 'Kho',
    icon: 'warehouse',
    entries: [
      { path: '/inventory/stock', label: 'Tồn kho', roles: ADMIN_ROLES },
      { path: '/inventory/ledger', label: 'Sổ giao dịch', roles: ADMIN_ROLES },
      { path: '/inventory/receipts', label: 'Phiếu nhập', roles: ADMIN_ROLES },
      { path: '/inventory/transfers', label: 'Chuyển kho', roles: ADMIN_ROLES },
      { path: '/inventory/stocktakes', label: 'Kiểm kê', roles: ADMIN_ROLES },
    ],
  },
  {
    key: 'orders',
    label: 'Đơn hàng',
    icon: 'order',
    entries: [
      { path: '/orders', label: 'Danh sách đơn', roles: ADMIN_ROLES },
      { path: '/orders/new', label: 'Tạo đơn', roles: ADMIN_ROLES },
    ],
  },
  {
    key: 'reports',
    label: 'Báo cáo',
    icon: 'report',
    entries: [
      { path: '/reports/gross-profit', label: 'Lợi nhuận gộp', roles: OWNER_ONLY },
      { path: '/reports/inventory', label: 'Tồn kho và tốc độ bán', roles: ADMIN_ROLES },
      { path: '/forecast', label: 'Dự báo nhập hàng', roles: OWNER_ONLY },
    ],
  },
  { key: 'channel', label: 'Kênh sàn', icon: 'channel', entries: [{ path: '/channel', label: 'Kênh sàn', roles: OWNER_ONLY }] },
  {
    key: 'settings',
    label: 'Cài đặt',
    icon: 'settings',
    entries: [
      { path: '/branches', label: 'Chi nhánh', roles: OWNER_ONLY },
      { path: '/users', label: 'Người dùng', roles: OWNER_ONLY },
    ],
  },
];

// Nhóm và mục mà vai trò được vào; nhóm không còn mục nào thì ẩn luôn.
export const menuFor = (role: Role | undefined) =>
  MENU.map((group) => ({ ...group, entries: visibleFor(group.entries, role) })).filter((group) => group.entries.length > 0);

// Mục menu của một đường dẫn: khớp đúng, hoặc đường dẫn là trang con của mục. So theo cả đoạn đường dẫn, để
// /inventory/stocktakes không khớp mục /inventory/stock; mục dài nhất thắng, để /orders/new không rơi vào /orders.
export function findEntry(pathname: string): { group: MenuGroup; entry: MenuEntry } | undefined {
  let found: { group: MenuGroup; entry: MenuEntry } | undefined;
  for (const group of MENU) {
    for (const entry of group.entries) {
      const matches = pathname === entry.path || (entry.path !== '/' && pathname.startsWith(`${entry.path}/`));
      if (matches && (!found || entry.path.length > found.entry.path.length)) found = { group, entry };
    }
  }
  return found;
}
