import { theme } from 'antd';

// Logo OISM giống admin: khối hộp xếp chồng trên nền màu chính, kèm chữ "POS" khi đủ chỗ.
export function Logo({ withText = true, size = 32 }: { withText?: boolean; size?: number }) {
  const { token } = theme.useToken();
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 10 }}>
      <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
        <rect width="32" height="32" rx="7" fill={token.colorPrimary} />
        <path d="M9 13.5 16 10l7 3.5-7 3.5-7-3.5Z" fill="#fff" />
        <path d="M9 17.5 16 21l7-3.5" fill="none" stroke="#fff" strokeWidth="1.8" strokeLinejoin="round" />
        <path d="M9 21 16 24.5 23 21" fill="none" stroke="#fff" strokeOpacity=".65" strokeWidth="1.8" strokeLinejoin="round" />
      </svg>
      {withText && (
        <span style={{ fontWeight: 700, fontSize: 20, letterSpacing: 0.5, color: token.colorText }} aria-label="OISM POS">
          OISM <span style={{ color: token.colorPrimary }}>POS</span>
        </span>
      )}
    </span>
  );
}
