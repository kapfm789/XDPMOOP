import type { ReactNode } from 'react';

// Icon của thanh bên và thanh trên, viết bằng SVG ở đây: dự án không thêm package icon (docs/design/ui/admin.md).
export type IconName =
  | 'dashboard'
  | 'product'
  | 'warehouse'
  | 'order'
  | 'report'
  | 'channel'
  | 'settings'
  | 'bell'
  | 'logout'
  | 'fold'
  | 'unfold';

const PATHS: Record<IconName, ReactNode> = {
  dashboard: (
    <>
      <rect x="3" y="3" width="7" height="9" rx="1.5" />
      <rect x="14" y="3" width="7" height="5" rx="1.5" />
      <rect x="14" y="12" width="7" height="9" rx="1.5" />
      <rect x="3" y="16" width="7" height="5" rx="1.5" />
    </>
  ),
  product: (
    <>
      <path d="M20.6 13.4 13.4 20.6a2 2 0 0 1-2.8 0L3 13V3h10l7.6 7.6a2 2 0 0 1 0 2.8Z" />
      <circle cx="7.5" cy="7.5" r="1.5" />
    </>
  ),
  warehouse: (
    <>
      <path d="M3 21V8l9-5 9 5v13" />
      <path d="M7 21v-8h10v8" />
      <path d="M7 17h10" />
    </>
  ),
  order: (
    <>
      <path d="M6 2h12l2 4v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6l2-4Z" />
      <path d="M4 6h16" />
      <path d="M9 11h6" />
      <path d="M9 15h6" />
    </>
  ),
  report: (
    <>
      <path d="M3 3v18h18" />
      <path d="M7 15l4-4 3 3 5-6" />
    </>
  ),
  channel: (
    <>
      <path d="M3 9l1.5-5h15L21 9" />
      <path d="M3 9a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0" />
      <path d="M5 12v9h14v-9" />
      <path d="M10 21v-5h4v5" />
    </>
  ),
  settings: (
    <>
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z" />
    </>
  ),
  bell: (
    <>
      <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
      <path d="M13.7 21a2 2 0 0 1-3.4 0" />
    </>
  ),
  logout: (
    <>
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <path d="M16 17l5-5-5-5" />
      <path d="M21 12H9" />
    </>
  ),
  fold: (
    <>
      <path d="M3 6h18" />
      <path d="M11 12h10" />
      <path d="M3 18h18" />
      <path d="M7 9l-3 3 3 3" />
    </>
  ),
  unfold: (
    <>
      <path d="M3 6h18" />
      <path d="M3 12h10" />
      <path d="M3 18h18" />
      <path d="M17 9l3 3-3 3" />
    </>
  ),
};

export function Icon({ name, size = 18 }: { name: IconName; size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      // Ant Design canh icon của Menu theo lớp này.
      className="anticon"
    >
      {PATHS[name]}
    </svg>
  );
}

// Logo OISM: khối hộp xếp chồng trên nền màu chính, kèm chữ khi đủ chỗ.
export function Logo({ withText = true, size = 32 }: { withText?: boolean; size?: number }) {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 10 }}>
      <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
        <rect width="32" height="32" rx="7" fill="#1677ff" />
        <path d="M9 13.5 16 10l7 3.5-7 3.5-7-3.5Z" fill="#fff" />
        <path d="M9 17.5 16 21l7-3.5" fill="none" stroke="#fff" strokeWidth="1.8" strokeLinejoin="round" />
        <path d="M9 21 16 24.5 23 21" fill="none" stroke="#fff" strokeOpacity=".65" strokeWidth="1.8" strokeLinejoin="round" />
      </svg>
      {withText && (
        <span style={{ fontWeight: 700, fontSize: 20, letterSpacing: 0.5, color: '#172033' }} aria-label="OISM">
          OISM
        </span>
      )}
    </span>
  );
}
