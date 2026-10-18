import type { ThemeConfig } from 'antd';

// Cùng bảng màu, bo góc và font với admin (docs/design/ui/pos.md mục "Bố cục và phong cách"). Chỉ đặt qua theme token
// của Ant Design, không viết CSS đè lên component.
export const COLORS = {
  primary: '#1677ff',
  primarySoft: '#e6f4ff',
  page: '#f5f7fa',
  card: '#ffffff',
  border: '#e4e9f0',
  text: '#172033',
  textSecondary: '#697386',
  success: '#52c41a',
  warning: '#fa8c16',
  error: '#f5222d',
} as const;

export const POS_THEME: ThemeConfig = {
  token: {
    colorPrimary: COLORS.primary,
    colorInfo: COLORS.primary,
    colorSuccess: COLORS.success,
    colorWarning: COLORS.warning,
    colorError: COLORS.error,
    colorBgLayout: COLORS.page,
    colorBgContainer: COLORS.card,
    colorBorder: COLORS.border,
    colorBorderSecondary: COLORS.border,
    colorText: COLORS.text,
    colorTextHeading: COLORS.text,
    colorTextSecondary: COLORS.textSecondary,
    colorTextDescription: COLORS.textSecondary,
    borderRadius: 6,
    borderRadiusLG: 8,
    borderRadiusSM: 6,
    // Vùng chạm tối thiểu 44 px: docs/conventions/frontend.md mục "Riêng cho POS".
    controlHeightLG: 44,
    fontFamily: "Inter, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
  },
  components: {
    Layout: { headerBg: COLORS.card, bodyBg: COLORS.page, headerPadding: '0 16px', headerHeight: 64 },
    Card: { headerFontSize: 15 },
  },
};
