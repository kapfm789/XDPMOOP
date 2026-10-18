import type { ThemeConfig } from 'antd';

// Màu, bo góc và font theo bản Figma của nhóm (docs/design/ui/admin.md mục "Bố cục và phong cách"). Chỉ đặt qua theme
// token của Ant Design, không viết CSS đè lên component.
export const COLORS = {
  primary: '#1677ff',
  page: '#f5f7fa',
  card: '#ffffff',
  border: '#e4e9f0',
  text: '#172033',
  textSecondary: '#697386',
  tableHeader: '#f7f9fc',
  success: '#52c41a',
  warning: '#fa8c16',
  error: '#f5222d',
} as const;

export const ADMIN_THEME: ThemeConfig = {
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
    borderRadiusLG: 7,
    borderRadiusSM: 6,
    fontFamily: "Inter, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
  },
  components: {
    Layout: { siderBg: COLORS.card, headerBg: COLORS.card, bodyBg: COLORS.page, headerPadding: '0 24px' },
    Menu: {
      itemBg: COLORS.card,
      subMenuItemBg: COLORS.card,
      itemSelectedBg: '#e6f4ff',
      itemSelectedColor: COLORS.primary,
      itemColor: COLORS.text,
      groupTitleColor: COLORS.textSecondary,
      itemBorderRadius: 6,
      activeBarBorderWidth: 0,
    },
    // Đầu bảng nền xám nhạt, dòng cao.
    Table: {
      headerBg: COLORS.tableHeader,
      headerColor: COLORS.textSecondary,
      headerSplitColor: 'transparent',
      cellPaddingBlock: 14,
      rowHoverBg: '#f5f9ff',
    },
    Card: { headerFontSize: 15 },
    Breadcrumb: { itemColor: COLORS.textSecondary, lastItemColor: COLORS.text },
  },
};
